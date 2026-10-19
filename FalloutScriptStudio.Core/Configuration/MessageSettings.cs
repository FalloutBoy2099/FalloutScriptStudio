namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// Options of message files and the Message Editor.
	/// </summary>
	public sealed class MessageSettings
	{
		/// <summary>Gets or sets the game language (the folder name below <c>text</c>).</summary>
		public String Language { get; set; } = "english";

		/// <summary>Gets or sets a value indicating whether the message file of a script is opened together with the script.</summary>
		public Boolean AutoOpenMessageFiles { get; set; }

		/// <summary>Gets or sets a value indicating whether the Message Editor is opened automatically with a script.</summary>
		public Boolean OpenEditorAutomatically { get; set; }

		/// <summary>Gets or sets a value indicating whether message files are associated through the script number in <c>scripts.lst</c>.</summary>
		public Boolean AssociateByScriptId { get; set; }

		/// <summary>Gets or sets a value indicating whether a script and its message file are compared when one of them is saved (the unknown, unused and duplicate messages are listed as problems).</summary>
		public Boolean CheckMessagesOnSave { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether comment lines are highlighted in the Message Editor.</summary>
		public Boolean HighlightComments { get; set; } = true;

		/// <summary>Gets or sets the highlight color of comment lines.</summary>
		public CommentHighlightColor CommentColor { get; set; } = CommentHighlightColor.LightYellow;

		/// <summary>Gets or sets the index of the selected font size in the Message Editor.</summary>
		public Int32 FontSizeIndex { get; set; }

		/// <summary>Gets or sets a value indicating whether the sound (lip) column is shown in the Message Editor.</summary>
		public Boolean ShowLipColumn { get; set; }

		/// <summary>Gets or sets a value indicating whether the script registration dialog enables the "allow" option for new scripts.</summary>
		public Boolean RegisterScriptAllowDefine { get; set; }
	}
}