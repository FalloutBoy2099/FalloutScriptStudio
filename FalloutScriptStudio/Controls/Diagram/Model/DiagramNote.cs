namespace FalloutScriptStudio.Controls.Diagram.Model
{
	using System;

	/// <summary>
	/// A free-floating sticky note with editable text. It does not take part in link routing.
	/// </summary>
	public sealed class DiagramNote : DiagramItem
	{
		private String _text = String.Empty;
		private Boolean _isEditing;

		/// <summary>
		/// Initializes a new note of 200 x 50 units.
		/// </summary>
		public DiagramNote()
		{
			this.Width = 200.0;
			this.Height = 50.0;
		}

		/// <summary>Gets or sets the note text. An empty text shows a placeholder.</summary>
		public String Text
		{
			get => this._text;
			set => this.SetProperty(ref this._text, value ?? String.Empty);
		}

		/// <summary>Gets a value indicating whether the in-place text editor is currently open for this note.</summary>
		public Boolean IsEditing => this._isEditing;

		/// <inheritdoc />
		public override Double MinimumWidth => 80.0;

		/// <inheritdoc />
		public override Double MinimumHeight => 30.0;

		/// <inheritdoc />
		public override Boolean CanResizeVertically => true;

		internal void SetEditing(Boolean value)
		{
			this.SetProperty(ref this._isEditing, value, nameof(this.IsEditing));
		}
	}
}
