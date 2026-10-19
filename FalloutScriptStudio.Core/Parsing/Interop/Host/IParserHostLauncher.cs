namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;

	/// <summary>
	/// Starts parser host processes for <see cref="OutOfProcessNativeParser"/>.
	/// </summary>
	public interface IParserHostLauncher
	{
		/// <summary>Starts a host.</summary>
		/// <param name="hostPath">Full path of <c>FalloutScriptStudio.ParserHost.exe</c>.</param>
		/// <param name="workingDirectory">An existing, private directory: <c>parser.dll</c> writes its log (<c>errors.txt</c>) to the working directory.</param>
		/// <returns>The running host.</returns>
		/// <exception cref="System.ComponentModel.Win32Exception">The process could not be started.</exception>
		IParserHostProcess Start(String hostPath, String workingDirectory);
	}
}