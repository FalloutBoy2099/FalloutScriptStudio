namespace FalloutScriptStudio.Services
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.IO.Pipes;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Makes the application single-instance per installation folder. A second instance forwards its
	/// command-line arguments to the first one through a named pipe and exits.
	/// </summary>
	public sealed class SingleInstanceService : IDisposable
	{
		/// <summary>How long a start-up of the first instance is waited for before the arguments are given up.</summary>
		public static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(30);

		private static readonly TimeSpan _connectAttempt = TimeSpan.FromMilliseconds(1000);
		private static readonly TimeSpan _retryPause = TimeSpan.FromMilliseconds(100);
		private static readonly TimeSpan _listenPause = TimeSpan.FromMilliseconds(500);
		private static readonly TimeSpan _maximumListenPause = TimeSpan.FromSeconds(10);
		private const Int32 MaximumListenFailures = 20;

		private readonly String _name;
		private readonly TimeSpan _sendTimeout;
		private readonly Action<String>? _diagnostics;
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private Mutex? _mutex;

		/// <summary>Initializes the service for the installation folder <paramref name="baseDirectory"/>.</summary>
		/// <param name="baseDirectory">The folder of the installation.</param>
		/// <param name="sendTimeout">How long <see cref="SendArgumentsAsync"/> waits for the first instance to be ready; <see cref="DefaultSendTimeout"/> when omitted.</param>
		/// <param name="diagnostics">Receives the problems of the pipe (written to the log by the application).</param>
		public SingleInstanceService(String baseDirectory, TimeSpan? sendTimeout = null, Action<String>? diagnostics = null)
		{
			Byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(baseDirectory.ToUpperInvariant()));
			this._name = "FalloutScriptStudio_" + Convert.ToHexString(hash, 0, 8);
			this._sendTimeout = sendTimeout ?? DefaultSendTimeout;
			this._diagnostics = diagnostics;
		}

		/// <summary>Raised on a thread-pool thread when another instance forwards arguments.</summary>
		public event EventHandler<String[]>? ArgumentsReceived;

		/// <summary>Tries to become the primary instance.</summary>
		/// <returns><see langword="true"/> when this is the first instance.</returns>
		public Boolean TryAcquire()
		{
			this._mutex = new Mutex(true, this._name, out Boolean createdNew);
			if (!createdNew)
			{
				this._mutex.Dispose();
				this._mutex = null;
			}

			return createdNew;
		}

		/// <summary>Starts listening for arguments sent by later instances (primary instance only).</summary>
		public void StartListening()
		{
			_ = Task.Run(() => this.ListenAsync(this._cancellation.Token));
		}

		/// <summary>
		/// Sends command-line arguments to the primary instance. The primary instance may still be starting (its pipe appears after its
		/// window), so the connection is retried until the send timeout is over.
		/// </summary>
		/// <param name="arguments">The arguments.</param>
		/// <param name="cancellationToken">Cancels the wait.</param>
		/// <returns><see langword="true"/> when the primary instance received the arguments.</returns>
		public async Task<Boolean> SendArgumentsAsync(String[] arguments, CancellationToken cancellationToken = default)
		{
			Stopwatch elapsed = Stopwatch.StartNew();
			while (true)
			{
				try
				{
					TimeSpan remaining = this._sendTimeout - elapsed.Elapsed;
					TimeSpan attempt = remaining < _connectAttempt ? remaining : _connectAttempt;
					await using NamedPipeClientStream client = new NamedPipeClientStream(".", this._name, PipeDirection.Out);
					await client.ConnectAsync(Math.Max(1, (Int32)attempt.TotalMilliseconds), cancellationToken).ConfigureAwait(false);
					await using StreamWriter writer = new StreamWriter(client, Encoding.UTF8);
					foreach (String argument in arguments)
					{
						String full = Path.IsPathRooted(argument) ? argument : Path.GetFullPath(argument);
						await writer.WriteLineAsync(full).ConfigureAwait(false);
					}

					return true;
				}
				catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
				{
					if (elapsed.Elapsed >= this._sendTimeout)
					{
						this._diagnostics?.Invoke("The arguments could not be passed to the running instance: " + ex.Message);
						return false;
					}

					await Task.Delay(_retryPause, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		public void Dispose()
		{
			this._cancellation.Cancel();
			this._cancellation.Dispose();
			if (this._mutex is not null)
			{
				try
				{
					this._mutex.ReleaseMutex();
				}
				catch (ApplicationException)
				{
					// Disposed from another thread than the one that took the lock; closing the handle below is all that can be done.
				}

				this._mutex.Dispose();
				this._mutex = null;
			}
		}

		private async Task ListenAsync(CancellationToken cancellationToken)
		{
			Int32 failures = 0;
			TimeSpan pause = _listenPause;
			while (!cancellationToken.IsCancellationRequested)
			{
				NamedPipeServerStream server;
				try
				{
					server = new NamedPipeServerStream(this._name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// A pipe that cannot be created (another user owns the name, too many instances) must neither spin the processor
					// nor end the listener at the first failure.
					failures++;
					if (failures >= MaximumListenFailures)
					{
						this._diagnostics?.Invoke("The pipe of the single-instance service cannot be used any more: " + ex.Message);
						return;
					}

					try
					{
						await Task.Delay(pause, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						return;
					}

					pause = TimeSpan.FromTicks(Math.Min(pause.Ticks * 2, _maximumListenPause.Ticks));
					continue;
				}

				failures = 0;
				pause = _listenPause;
				await using (server.ConfigureAwait(false))
				{
					try
					{
						await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
						using StreamReader reader = new StreamReader(server, Encoding.UTF8);

						List<String> lines = new List<String>();
						String? line;
						while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
						{
							lines.Add(line);
						}

						this.ArgumentsReceived?.Invoke(this, lines.ToArray());
					}
					catch (OperationCanceledException)
					{
						return;
					}
					catch (IOException)
					{
						// A client disconnected abruptly; keep listening.
					}
					catch (Exception ex) when (ex is not OutOfMemoryException)
					{
						// A handler of ArgumentsReceived that throws must not stop the listener.
						this._diagnostics?.Invoke("Handling the forwarded arguments failed: " + ex.Message);
					}
				}
			}
		}
	}
}
