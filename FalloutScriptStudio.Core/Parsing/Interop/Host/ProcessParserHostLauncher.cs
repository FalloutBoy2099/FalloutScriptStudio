namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// <see cref="IParserHostLauncher"/> that starts the real <c>FalloutScriptStudio.ParserHost.exe</c> process.
	/// </summary>
	public sealed class ProcessParserHostLauncher : IParserHostLauncher
	{
		private readonly ILogger _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessParserHostLauncher"/> class.
		/// </summary>
		/// <param name="logger">Optional logger.</param>
		public ProcessParserHostLauncher(ILogger<ProcessParserHostLauncher>? logger = null)
		{
			this._logger = logger ?? NullLogger<ProcessParserHostLauncher>.Instance;
		}

		/// <inheritdoc />
		public IParserHostProcess Start(String hostPath, String workingDirectory)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(hostPath);
			ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
			return ProcessParserHost.Start(hostPath, workingDirectory, this._logger);
		}
	}
}
