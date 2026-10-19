namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Channels;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;

	/// <summary>
	/// An in-memory stand-in for a parser host process. The test decides what the "process" does with every request line: answer,
	/// stay silent (hang), print garbage, or die with an exit code.
	/// </summary>
	internal sealed class FakeParserHost : IParserHostProcess
	{
		private readonly Channel<String?> _output = Channel.CreateUnbounded<String?>();
		private readonly TaskCompletionSource _exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _fatalError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		private Int32? _exitCode;

		public FakeParserHost(Action<FakeParserHost, String> onRequest)
		{
			this.OnRequest = onRequest;
			this.Input = new RequestWriter(this);
			this.Output = new ChannelLineReader(this._output.Reader);
		}

		/// <summary>Gets the handler invoked for every request line.</summary>
		public Action<FakeParserHost, String> OnRequest { get; }

		/// <summary>Gets the request lines received so far.</summary>
		public List<String> Requests { get; } = [];

		/// <summary>Gets or sets a value indicating whether writing a request throws an <see cref="IOException"/> (broken pipe).</summary>
		public Boolean FailWrites { get; set; }

		/// <summary>Gets a value indicating whether <see cref="Kill"/> was called.</summary>
		public Boolean Killed { get; private set; }

		/// <summary>Gets a value indicating whether <see cref="Dispose"/> was called.</summary>
		public Boolean Disposed { get; private set; }

		public TextWriter Input { get; }

		public TextReader Output { get; }

		public Boolean HasExited => this._exitCode.HasValue;

		public Int32? ExitCode => this._exitCode;

		public String StandardErrorTail { get; set; } = String.Empty;

		public Task FatalErrorReported => this._fatalError.Task;

		/// <summary>Simulates the runtime printing a fatal-error report: the error stream gets the text and the host stays alive (hangs in crash reporting).</summary>
		public void ReportFatalError(String errorOutput)
		{
			this.StandardErrorTail = errorOutput;
			this._fatalError.TrySetResult();
		}

		/// <summary>Makes the host print a line to its standard output.</summary>
		public void Emit(String line)
		{
			this._output.Writer.TryWrite(line);
		}

		/// <summary>Makes the host answer a request with a result.</summary>
		public void Respond(HostRequest request, NativeParseResult result)
		{
			this.Emit(HostProtocol.SerializeResponse(HostMapper.ToResponse(request.Id, result)));
		}

		/// <summary>Makes the process end: its output closes and its exit code becomes available.</summary>
		public void Exit(Int32 exitCode)
		{
			this._exitCode ??= exitCode;
			this._output.Writer.TryComplete();
			this._exited.TrySetResult();
		}

		public void Kill()
		{
			this.Killed = true;
			this.Exit(-1);
		}

		public Task WaitForExitAsync(CancellationToken cancellationToken)
		{
			return this._exited.Task.WaitAsync(cancellationToken);
		}

		public void Dispose()
		{
			this.Disposed = true;
		}

		private sealed class RequestWriter : TextWriter
		{
			private readonly FakeParserHost _host;

			public RequestWriter(FakeParserHost host)
			{
				this._host = host;
			}

			public override Encoding Encoding => Encoding.UTF8;

			public override Task WriteLineAsync(String? value)
			{
				if (this._host.FailWrites || this._host.HasExited)
				{
					throw new IOException("The pipe has been ended.");
				}

				this._host.Requests.Add(value ?? String.Empty);
				this._host.OnRequest(this._host, value ?? String.Empty);
				return Task.CompletedTask;
			}

			public override Task FlushAsync()
			{
				return Task.CompletedTask;
			}
		}

		private sealed class ChannelLineReader : TextReader
		{
			private readonly ChannelReader<String?> _reader;

			public ChannelLineReader(ChannelReader<String?> reader)
			{
				this._reader = reader;
			}

			public override async Task<String?> ReadLineAsync()
			{
				return await this._reader.WaitToReadAsync().ConfigureAwait(false) && this._reader.TryRead(out String? line) ? line : null;
			}
		}
	}
}