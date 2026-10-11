namespace FalloutScriptStudio.Services
{
	using System;
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
		private readonly String _name;
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private Mutex? _mutex;

		/// <summary>Initializes the service for the installation folder <paramref name="baseDirectory"/>.</summary>
		public SingleInstanceService(String baseDirectory)
		{
			Byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(baseDirectory.ToUpperInvariant()));
			this._name = "FalloutScriptStudio_" + Convert.ToHexString(hash, 0, 8);
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

		/// <summary>Sends command-line arguments to the primary instance.</summary>
		public async Task<Boolean> SendArgumentsAsync(String[] arguments)
		{
			try
			{
				await using NamedPipeClientStream client = new NamedPipeClientStream(".", this._name, PipeDirection.Out);
				await client.ConnectAsync(3000);
				await using StreamWriter writer = new StreamWriter(client, Encoding.UTF8);
				foreach (String argument in arguments)
				{
					String full = Path.IsPathRooted(argument) ? argument : Path.GetFullPath(argument);
					await writer.WriteLineAsync(full);
				}

				return true;
			}
			catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
			{
				return false;
			}
		}

		public void Dispose()
		{
			this._cancellation.Cancel();
			this._cancellation.Dispose();
			if (this._mutex is not null)
			{
				this._mutex.ReleaseMutex();
				this._mutex.Dispose();
				this._mutex = null;
			}
		}

		private async Task ListenAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					await using NamedPipeServerStream server = new NamedPipeServerStream(this._name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
					await server.WaitForConnectionAsync(cancellationToken);
					using StreamReader reader = new StreamReader(server, Encoding.UTF8);

					System.Collections.Generic.List<String> lines = new System.Collections.Generic.List<String>();
					String? line;
					while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
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
			}
		}
	}
}
