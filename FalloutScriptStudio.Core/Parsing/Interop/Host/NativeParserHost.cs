namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.IO;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// The request/response loop of <c>FalloutScriptStudio.ParserHost.exe</c>: reads requests from a reader, runs them on an
	/// <see cref="INativeParser"/> (the in-process <see cref="NativeParser"/> in the real host) and writes the responses.
	/// </summary>
	/// <remarks>
	/// The loop is kept apart from the executable so that the protocol can be tested with in-memory streams. It never throws
	/// because of a bad request: a request that cannot be understood is answered with a
	/// <see cref="NativeParseStatus.Failed"/> response. See <see cref="HostProtocol"/> for the wire format.
	/// </remarks>
	public sealed class NativeParserHost
	{
		private readonly INativeParser _engine;

		/// <summary>
		/// Initializes a new instance of the <see cref="NativeParserHost"/> class.
		/// </summary>
		/// <param name="engine">The parser that does the work.</param>
		public NativeParserHost(INativeParser engine)
		{
			ArgumentNullException.ThrowIfNull(engine);
			this._engine = engine;
		}

		/// <summary>
		/// Serves requests until the input ends, an <c>exit</c> request arrives or the operation is cancelled.
		/// </summary>
		/// <param name="input">The request lines.</param>
		/// <param name="output">Receives the response lines.</param>
		/// <param name="cancellationToken">Stops the loop between two requests.</param>
		/// <returns>The number of parse requests that were answered.</returns>
		public async Task<Int32> RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(input);
			ArgumentNullException.ThrowIfNull(output);

			Int32 served = 0;
			while (!cancellationToken.IsCancellationRequested)
			{
				String? line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
				if (line is null)
				{
					break;
				}

				if (line.Length == 0)
				{
					continue;
				}

				HostResponse response;
				Boolean exit = false;
				try
				{
					HostRequest request = HostProtocol.DeserializeRequest(line);
					if (request.Version != HostProtocol.Version)
					{
						response = Failure(request.Id, "Unsupported protocol version " + request.Version + " (expected " + HostProtocol.Version + ").");
					}
					else if (String.Equals(request.Command, HostProtocol.ExitCommand, StringComparison.Ordinal))
					{
						exit = true;
						response = new HostResponse { Id = request.Id, Status = nameof(NativeParseStatus.Completed), ExitCode = 0 };
					}
					else if (String.Equals(request.Command, HostProtocol.PingCommand, StringComparison.Ordinal))
					{
						response = new HostResponse { Id = request.Id, Status = nameof(NativeParseStatus.Completed), ExitCode = 0 };
					}
					else if (String.Equals(request.Command, HostProtocol.ParseCommand, StringComparison.Ordinal))
					{
						response = await this.ParseAsync(request, cancellationToken).ConfigureAwait(false);
						served++;
					}
					else
					{
						response = Failure(request.Id, "Unknown command '" + request.Command + "'.");
					}
				}
				catch (JsonException ex)
				{
					response = Failure(0, "The request is not valid JSON: " + ex.Message);
				}

				await output.WriteAsync(HostProtocol.SerializeResponse(response) + "\n").ConfigureAwait(false);
				await output.FlushAsync(cancellationToken).ConfigureAwait(false);

				if (exit)
				{
					break;
				}
			}

			return served;
		}

		private static HostResponse Failure(Int64 id, String message)
		{
			return new HostResponse { Id = id, Status = nameof(NativeParseStatus.Failed), Message = message };
		}

		private async Task<HostResponse> ParseAsync(HostRequest request, CancellationToken cancellationToken)
		{
			try
			{
				NativeParseResult result = await this._engine
					.ParseAsync(request.ScriptText, request.FilePath, HostMapper.ToOptions(request), cancellationToken)
					.ConfigureAwait(false);
				return HostMapper.ToResponse(request.Id, result);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				return Failure(request.Id, "The native parser failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}
}
