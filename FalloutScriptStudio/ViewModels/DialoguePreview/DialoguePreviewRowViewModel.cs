namespace FalloutScriptStudio.ViewModels.DialoguePreview
{
	using System;
	using FalloutScriptStudio.Core.Dialogue;

	/// <summary>
	/// A row of the dialogue preview grid: an NPC text, an option or a jump of the node being shown.
	/// </summary>
	public sealed class DialoguePreviewRowViewModel
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DialoguePreviewRowViewModel"/> class.
		/// </summary>
		/// <param name="source">The row made by the preview.</param>
		public DialoguePreviewRowViewModel(DialoguePreviewRow source)
		{
			ArgumentNullException.ThrowIfNull(source);
			this.Source = source;
		}

		/// <summary>Gets the row made by the preview.</summary>
		public DialoguePreviewRow Source { get; }

		/// <summary>Gets the node an option leads to, or <c>[Reply]</c> / <c>[Message]</c>.</summary>
		public String Target => this.Source.TargetNode;

		/// <summary>Gets the text of the message column (options start with a bullet).</summary>
		public String Text => this.Source.DisplayText;

		/// <summary>Gets the text a screen reader announces for the row (the automation name of a list or grid item is the text of its object).</summary>
		/// <returns>The text.</returns>
		public override String ToString()
		{
			return this.Text;
		}

		/// <summary>Gets the IQ requirement of an option, or an empty text.</summary>
		public String Iq => this.Source.Iq ?? String.Empty;

		/// <summary>Gets the message number shown in the line column, or an empty text when the statement has none.</summary>
		public String LineText => this.Source.MessageNumber > 0 ? this.Source.MessageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : String.Empty;

		/// <summary>Gets a value indicating whether the message text is an error text (shown in red).</summary>
		public Boolean IsError => this.Source.IsError;

		/// <summary>Gets a value indicating whether the row is a player option (shown in the accent color).</summary>
		public Boolean IsOption => this.Source.IsOption;

		/// <summary>Gets a value indicating whether the row leads to another node.</summary>
		public Boolean CanFollow => this.Source.CanFollow;

		/// <summary>Gets a value indicating whether the line number opens the message editor.</summary>
		public Boolean CanEditMessage => this.Source.MessageNumber > 0 && this.Source.MessageFilePath is not null;
	}
}