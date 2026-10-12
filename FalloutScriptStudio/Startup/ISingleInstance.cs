namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>The single-instance lock of an installation and the channel that carries the command line to the first instance.</summary>
	public interface ISingleInstance : IDisposable
	{
		/// <summary>Raised on a thread-pool thread when another instance forwards arguments.</summary>
		event EventHandler<String[]>? ArgumentsReceived;

		/// <summary>Tries to become the primary instance.</summary>
		/// <returns><see langword="true"/> when this is the first instance.</returns>
		Boolean TryAcquire();

		/// <summary>Starts listening for arguments sent by later instances (primary instance only).</summary>
		void StartListening();

		/// <summary>Sends command-line arguments to the primary instance.</summary>
		/// <param name="arguments">The arguments.</param>
		/// <param name="cancellationToken">Cancels the wait.</param>
		/// <returns><see langword="true"/> when the primary instance received the arguments.</returns>
		Task<Boolean> SendArgumentsAsync(String[] arguments, CancellationToken cancellationToken = default);
	}
}
