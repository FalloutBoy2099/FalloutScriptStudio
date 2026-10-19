namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// One line of the code of a dialogue node, with the dialogue statement found on it (legacy <c>DialogueParser</c> instance).
	/// </summary>
	public sealed class DialogueLine
	{
		/// <summary>Gets the kind of statement. <see cref="DialogueOpcode.Reply"/>, <see cref="DialogueOpcode.Message"/>, the option kinds and <see cref="DialogueOpcode.Call"/> are reported; <see cref="DialogueOpcode.None"/> marks a plain line of code.</summary>
		public DialogueOpcode Opcode { get; init; }

		/// <summary>Gets the line of code as written in the script (without trailing white space).</summary>
		public String Code { get; init; } = String.Empty;

		/// <summary>Gets the zero-based index of the line in the text that was parsed (the index of the editor line when empty lines are kept).</summary>
		public Int32 CodeLineIndex { get; init; }

		/// <summary>Gets the part of the line that holds the statement, from the function name to the closing parenthesis; <c>null</c> for plain code.</summary>
		public String? ShortCode { get; init; }

		/// <summary>
		/// Gets the name of the node the statement jumps to, or <c>[Reply]</c> / <c>[Message]</c> for NPC texts; <c>null</c> for plain code.
		/// </summary>
		public String? ToNode { get; init; }

		/// <summary>Gets the IQ requirement of an option as text (<c>-</c> when the function has no IQ argument), or <c>null</c> when it is not a number.</summary>
		public String? Iq { get; init; }

		/// <summary>Gets the message number, 0 when it could not be determined, or -1 for calls.</summary>
		public Int32 MessageNumber { get; init; }

		/// <summary>Gets the number of the message file (index in <c>scripts.lst</c>), or -1 for the message file of the script itself.</summary>
		public Int32 MessageFileNumber { get; init; } = -1;

		/// <summary>Gets a value indicating whether the statement is a player option.</summary>
		public Boolean IsOption => this.Opcode.IsOption();

		/// <summary>Gets a value indicating whether the statement is an NPC reply or message.</summary>
		public Boolean IsNpcText => this.Opcode.IsNpcText();

		/// <summary>Gets a value indicating whether the statement jumps to another node (an option or a call).</summary>
		public Boolean IsTransition => this.Opcode.IsTransition();

		/// <summary>Gets a value indicating whether the line shows a message whose number is known.</summary>
		public Boolean HasMessageNumber => this.Opcode.HasMessage() && this.MessageNumber > 0;

		/// <summary>Gets a value indicating whether the message is in another message file than the script's own.</summary>
		public Boolean UsesOtherMessageFile => this.MessageFileNumber > 0;

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Opcode + ": " + (this.ShortCode ?? this.Code);
		}
	}
}