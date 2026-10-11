namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// How the code of a node is parsed.
	/// </summary>
	public sealed record DialogueNodeParseOptions
	{
		/// <summary>
		/// Gets a value indicating whether lines without a dialogue statement are returned as <see cref="DialogueOpcode.None"/> lines
		/// (the flowchart shows the whole code of a node).
		/// </summary>
		public Boolean IncludePlainCode { get; init; }

		/// <summary>
		/// Gets a value indicating whether commented code is ignored: whole-line and block comments are skipped and statements in a
		/// trailing <c>//</c> comment are not parsed.
		/// </summary>
		public Boolean ExcludeComments { get; init; } = true;
	}
}
