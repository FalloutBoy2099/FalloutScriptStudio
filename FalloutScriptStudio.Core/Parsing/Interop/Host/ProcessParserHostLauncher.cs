namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;

	/// <summary>
	/// <see cref="IParserHostLauncher"/> that starts the real <c>FalloutScriptStudio.ParserHost.exe</c> process.
	/// </summary>
	public sealed class ProcessParserHostLauncher : IParserHostLauncher
	{
		/// <inheritdoc />
		public IParserHostProcess Start(String hostPath, String workingDirectory)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(hostPath);
			ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
			return ProcessParserHost.Start(hostPath, workingDirectory);
		}
	}
}
