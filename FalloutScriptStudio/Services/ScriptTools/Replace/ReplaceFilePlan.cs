namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The replacements of one file (or open document). The text of the file is not kept: the plan holds a SHA-256 of what was read, and the
	/// file is read again when the replacements are applied; it is replaced only when it still has that hash.
	/// </summary>
	/// <param name="Path">The full path of the file, or <see cref="FalloutScriptStudio.Core.Compilation.BuildError.NoFile"/> for a document that was never saved.</param>
	/// <param name="Hash">The SHA-256 of the bytes of the file (of the characters of the text for an open document).</param>
	/// <param name="Matches">The replacements, in the order of the file.</param>
	/// <param name="Owner">The open document the text belongs to, or <see langword="null"/> for a file that is not open.</param>
	public sealed record ReplaceFilePlan(String Path, Byte[] Hash, IReadOnlyList<ReplaceMatch> Matches, Object? Owner)
	{
		/// <summary>Gets a value indicating whether the replacements are made in an open document.</summary>
		public Boolean IsOpenDocument => this.Owner is not null;
	}
}