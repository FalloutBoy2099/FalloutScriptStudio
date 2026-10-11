namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Abstraction of the native (<c>parser.dll</c>) parser so that the orchestration can be tested and replaced.
	/// </summary>
	public interface INativeParser
	{
		/// <summary>
		/// Parses a script with the native parser. The method never throws because of the native library:
		/// problems are reported through <see cref="NativeParseResult.Status"/>.
		/// </summary>
		/// <param name="scriptText">The current text of the script.</param>
		/// <param name="filePath">Full path of the script.</param>
		/// <param name="options">The current options.</param>
		/// <param name="cancellationToken">Cancels waiting for the native parser; a call that already started cannot be interrupted.</param>
		/// <returns>The result.</returns>
		Task<NativeParseResult> ParseAsync(String scriptText, String filePath, ParserOptions options, CancellationToken cancellationToken);
	}
}
