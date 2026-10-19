namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// One row of the dialogue preview: an NPC text, an option or a jump of the current node.
	/// </summary>
	public sealed class DialoguePreviewRow
	{
		/// <summary>The character the legacy preview put in front of option texts.</summary>
		public const Char OptionBullet = '●';

		/// <summary>Gets the parsed line the row was made from.</summary>
		public required DialogueLine Line { get; init; }

		/// <summary>Gets the kind of statement.</summary>
		public DialogueOpcode Opcode => this.Line.Opcode;

		/// <summary>
		/// Gets the target of the row as the legacy grid showed it: the node an option or call leads to, or <c>[Reply]</c> / <c>[Message]</c>.
		/// </summary>
		public String TargetNode { get; init; } = String.Empty;

		/// <summary>Gets the message text, an error text, or <c>&lt;short code&gt;</c> when the statement has no message number.</summary>
		public String MessageText { get; init; } = String.Empty;

		/// <summary>Gets the IQ requirement of an option as text, or <c>null</c>.</summary>
		public String? Iq { get; init; }

		/// <summary>Gets the message number shown in the number column (the number of the message that was found, including the female offset).</summary>
		public Int32 MessageNumber { get; init; }

		/// <summary>Gets the number of the message file (-1 for the script's own file).</summary>
		public Int32 MessageFileNumber => this.Line.MessageFileNumber;

		/// <summary>Gets the path of the message file that holds the message, or <c>null</c>; use it to open the message editor.</summary>
		public String? MessageFilePath { get; init; }

		/// <summary>Gets how the message lookup ended.</summary>
		public DialogueMessageStatus Status { get; init; }

		/// <summary>Gets a value indicating whether the message text is an error text (shown in red).</summary>
		public Boolean IsError => this.Status is DialogueMessageStatus.FileNotFound or DialogueMessageStatus.MessageNotFound;

		/// <summary>Gets a value indicating whether the row is a player option.</summary>
		public Boolean IsOption => this.Line.IsOption;

		/// <summary>Gets a value indicating whether double-clicking the row can lead to another node: an option or a call whose target node exists.</summary>
		public Boolean CanFollow { get; init; }

		/// <summary>Gets the text for the message column: options get a bullet in front.</summary>
		public String DisplayText => this.IsOption ? OptionBullet + " " + this.MessageText : this.MessageText;
	}
}