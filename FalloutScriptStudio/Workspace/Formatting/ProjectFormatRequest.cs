namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;

	/// <summary>What "Format Project" is asked to look at.</summary>
	/// <param name="Scope">Which files.</param>
	/// <param name="Masks">The file masks of the project folder scope, separated by <c>;</c> (for example <c>*.ssl;*.h</c>).</param>
	public sealed record ProjectFormatRequest(FormatScope Scope, String Masks)
	{
		/// <summary>The masks of the scripts and the headers.</summary>
		public const String DefaultMasks = "*.ssl;*.h";

		/// <summary>Gets the largest file that is formatted; a bigger one is reported and left alone.</summary>
		public Int64 MaxFileSize { get; init; } = 5L * 1024 * 1024;
	}
}