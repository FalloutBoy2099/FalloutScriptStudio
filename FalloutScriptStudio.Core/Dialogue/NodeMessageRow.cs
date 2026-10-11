namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// A message used by the code of a node, as listed by the node code editor (legacy <c>FlowchartTE</c> message grid).
	/// </summary>
	/// <param name="Line">The parsed statement.</param>
	/// <param name="Lookup">The message with its file and status; an error status means the text is an error text.</param>
	public sealed record NodeMessageRow(DialogueLine Line, DialogueMessageLookup Lookup)
	{
		/// <summary>Gets the message number.</summary>
		public Int32 MessageNumber => this.Line.MessageNumber;

		/// <summary>Gets the message text (or the error text).</summary>
		public String Text => this.Lookup.Text ?? String.Empty;

		/// <summary>Gets the path of the message file, or <c>null</c> when it is missing.</summary>
		public String? MessageFilePath => this.Lookup.FilePath;

		/// <summary>Gets the zero-based index of the line of the statement in the node code.</summary>
		public Int32 CodeLineIndex => this.Line.CodeLineIndex;

		/// <summary>Gets a value indicating whether the message is a player option (shown in blue by the legacy grid).</summary>
		public Boolean IsOption => this.Line.IsOption;

		/// <summary>Gets a value indicating whether the message could not be read (shown in red); its text cannot be edited.</summary>
		public Boolean IsError => this.Lookup.IsError;
	}
}
