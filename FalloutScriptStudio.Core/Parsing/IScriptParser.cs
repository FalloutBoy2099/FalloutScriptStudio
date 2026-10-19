namespace FalloutScriptStudio.Core.Parsing
{
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Orchestrates parsing of a script: native parser when possible, internal parser as fall-back, macros always.
	/// </summary>
	public interface IScriptParser
	{
		/// <summary>Gets the global macros shared by all scripts.</summary>
		MacroRegistry GlobalMacros { get; }

		/// <summary>
		/// Parses a script. The request is never modified; a new <see cref="ProgramInfo"/> is returned.
		/// </summary>
		/// <param name="request">What to parse.</param>
		/// <param name="cancellationToken">Cancels the operation (an already started native call cannot be interrupted).</param>
		/// <returns>The result; it is produced even when the native parser is missing or fails.</returns>
		Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken);

		/// <summary>
		/// Reloads the global macros from the headers directory of the current options.
		/// </summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the registry has been reloaded.</returns>
		Task ReloadGlobalMacrosAsync(CancellationToken cancellationToken);
	}
}