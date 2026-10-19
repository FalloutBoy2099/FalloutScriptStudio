namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Globalization;

	/// <summary>
	/// One entry of a <c>.msg</c> file: a message, a comment, an empty line or an unrecognized line.
	/// A message may span several physical lines; its <see cref="Text"/> then contains line breaks.
	/// </summary>
	public sealed class MessageEntry
	{
		/// <summary>The line break used inside multi-line message texts.</summary>
		public const String LineBreak = "\r\n";

		/// <summary>Gets or sets the kind of the entry.</summary>
		public MessageLineKind Kind { get; set; } = MessageLineKind.Blank;

		/// <summary>Gets or sets the message number (only meaningful for <see cref="MessageLineKind.Message"/>).</summary>
		public Int32 Id { get; set; }

		/// <summary>Gets or sets the sound (lip-sync) file name, the second brace group of a message.</summary>
		public String SoundName { get; set; } = String.Empty;

		/// <summary>
		/// Gets or sets the message text, or the whole line for comments and unrecognized lines.
		/// </summary>
		public String Text { get; set; } = String.Empty;

		/// <summary>Gets or sets the text that follows the closing brace of a message (usually a trailing <c># comment</c>).</summary>
		public String Description { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the message is a player (PC) line, marked by leading tabs.</summary>
		public Boolean IsPlayerLine { get; set; }

		/// <summary>Gets or sets the number of leading tabs of a player line.</summary>
		public Int32 IndentTabs { get; set; } = 1;

		/// <summary>Creates a message entry.</summary>
		/// <param name="id">The message number.</param>
		/// <param name="text">The message text.</param>
		/// <param name="soundName">The sound file name.</param>
		/// <returns>The new entry.</returns>
		public static MessageEntry CreateMessage(Int32 id, String text = "", String soundName = "")
		{
			return new MessageEntry { Kind = MessageLineKind.Message, Id = id, Text = text, SoundName = soundName };
		}

		/// <summary>Creates a comment entry.</summary>
		/// <param name="text">The comment line, normally starting with <c>#</c>.</param>
		/// <returns>The new entry.</returns>
		public static MessageEntry CreateComment(String text)
		{
			return new MessageEntry { Kind = MessageLineKind.Comment, Text = text };
		}

		/// <summary>Creates an empty-line entry.</summary>
		/// <returns>The new entry.</returns>
		public static MessageEntry CreateBlank()
		{
			return new MessageEntry { Kind = MessageLineKind.Blank };
		}

		/// <summary>
		/// Formats the entry the way it is stored in the file. Multi-line texts produce a string with line breaks.
		/// </summary>
		/// <returns>The file representation of the entry.</returns>
		public String Format()
		{
			if (this.Kind != MessageLineKind.Message)
			{
				return this.Kind == MessageLineKind.Blank ? String.Empty : this.Text + this.Description;
			}

			String indent = this.IsPlayerLine ? new String('\t', Math.Max(1, this.IndentTabs)) : String.Empty;
			return indent + "{" + this.Id.ToString(CultureInfo.InvariantCulture) + "}{" + this.SoundName + "}{" + this.Text + "}" + this.Description;
		}

		/// <inheritdoc/>
		public override String ToString()
		{
			return this.Format();
		}
	}
}