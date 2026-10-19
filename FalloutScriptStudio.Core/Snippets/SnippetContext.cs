namespace FalloutScriptStudio.Core.Snippets
{
	using System;

	/// <summary>Where and how a snippet is inserted: the indentation, the line break of the document and the values of the variables.</summary>
	public sealed record SnippetContext
	{
		/// <summary>Gets one indentation level as the document is indented (a tab, or spaces). A tab at the start of a line of a snippet body stands for one level.</summary>
		public String IndentUnit { get; init; } = "   ";

		/// <summary>Gets the white space the line of the insertion starts with; the lines after the first one of the snippet start with it too.</summary>
		public String LineIndent { get; init; } = String.Empty;

		/// <summary>Gets the line break of the document.</summary>
		public String NewLine { get; init; } = "\r\n";

		/// <summary>Gets the selected text, or <see langword="null"/> when nothing is selected (the value of <c>$SELECTION</c>).</summary>
		public String? Selection { get; init; }

		/// <summary>Gets the procedure the caret is in (the value of <c>$CURRENT_PROCEDURE</c>).</summary>
		public String CurrentProcedure { get; init; } = String.Empty;

		/// <summary>Gets the name of the file with its extension (the value of <c>$FILE_NAME</c>).</summary>
		public String FileName { get; init; } = String.Empty;

		/// <summary>Gets the name of the script without its extension (the value of <c>$SCRIPT_NAME</c>).</summary>
		public String ScriptName { get; init; } = String.Empty;

		/// <summary>Gets the time used for <c>$DATE</c> and <c>$TIME</c>.</summary>
		public DateTime Now { get; init; } = DateTime.Now;
	}
}