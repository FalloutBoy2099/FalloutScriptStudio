namespace FalloutScriptStudio.Controls.Diagram.Model
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>
	/// One line of a <see cref="DiagramNode"/>. A row shows <see cref="Text"/> and may carry a collapsible
	/// <see cref="Detail"/> line (typically the script code behind a message). A row can also be the origin of a
	/// connector line to another node through <see cref="LinkTargetName"/>.
	/// </summary>
	public sealed class DiagramRow : ObservableObject
	{
		private DiagramRowKind _kind;
		private DiagramRowIcon _icon;
		private String _text;
		private String? _detail;
		private Boolean _isExpanded;
		private String? _linkTargetName;
		private Object? _tag;

		/// <summary>
		/// Initializes a new row.
		/// </summary>
		/// <param name="kind">Visual style of the row.</param>
		/// <param name="text">Displayed text.</param>
		/// <param name="detail">Optional collapsible detail line, or <c>null</c>.</param>
		public DiagramRow(DiagramRowKind kind, String text, String? detail = null)
		{
			this._kind = kind;
			this._text = text ?? String.Empty;
			this._detail = detail;
			this._icon = kind switch
			{
				DiagramRowKind.Reply => DiagramRowIcon.Reply,
				DiagramRowKind.Option => DiagramRowIcon.Option,
				DiagramRowKind.Call => DiagramRowIcon.Call,
				_ => DiagramRowIcon.None
			};
		}

		/// <summary>Gets the node that owns this row, or <c>null</c> while the row is not in a node.</summary>
		public DiagramNode? Owner { get; internal set; }

		/// <summary>Gets or sets the visual style of the row.</summary>
		public DiagramRowKind Kind
		{
			get => this._kind;
			set => this.SetProperty(ref this._kind, value);
		}

		/// <summary>Gets or sets the icon drawn before the text. Defaults to a glyph matching <see cref="Kind"/>.</summary>
		public DiagramRowIcon Icon
		{
			get => this._icon;
			set => this.SetProperty(ref this._icon, value);
		}

		/// <summary>Gets or sets the displayed text (already formatted, e.g. with quotes).</summary>
		public String Text
		{
			get => this._text;
			set => this.SetProperty(ref this._text, value ?? String.Empty);
		}

		/// <summary>Gets or sets the collapsible detail line. <c>null</c> or empty makes the row non expandable.</summary>
		public String? Detail
		{
			get => this._detail;
			set
			{
				if (this.SetProperty(ref this._detail, value))
				{
					this.OnPropertyChanged(nameof(this.IsExpandable));
				}
			}
		}

		/// <summary>Gets a value indicating whether the row has a <see cref="Detail"/> line that can be expanded.</summary>
		public Boolean IsExpandable => !String.IsNullOrEmpty(this._detail);

		/// <summary>Gets or sets a value indicating whether the <see cref="Detail"/> line is currently shown.</summary>
		public Boolean IsExpanded
		{
			get => this._isExpanded;
			set => this.SetProperty(ref this._isExpanded, value);
		}

		/// <summary>
		/// Gets or sets the name of the node this row links to. When it matches the <see cref="DiagramNode.Name"/> of a node
		/// in the same canvas (case-insensitive) a <see cref="DiagramLink"/> starting at this row is created automatically.
		/// </summary>
		public String? LinkTargetName
		{
			get => this._linkTargetName;
			set => this.SetProperty(ref this._linkTargetName, value);
		}

		/// <summary>Gets or sets arbitrary caller data (for example the index of the source statement).</summary>
		public Object? Tag
		{
			get => this._tag;
			set => this.SetProperty(ref this._tag, value);
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return this._text;
		}
	}
}