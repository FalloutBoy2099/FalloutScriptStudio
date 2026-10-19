namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Input of <see cref="IScriptParser.ParseAsync"/>.
	/// </summary>
	/// <param name="ScriptText">The current text of the script.</param>
	public sealed record ParseRequest(String ScriptText)
	{
		/// <summary>Gets the path of the script; <c>null</c> for an unsaved script (only macros are collected then).</summary>
		public String? FilePath { get; init; }

		/// <summary>
		/// Gets the result of the previous parse of the same script. It is kept (with updated procedure locations) when the
		/// native parser cannot produce new data.
		/// </summary>
		public ProgramInfo? PreviousInfo { get; init; }

		/// <summary>Gets a value indicating whether this is the first parse after the script was opened (the global macros are reloaded).</summary>
		public Boolean IsFirstPass { get; init; }
	}
}