namespace FalloutScriptStudio.Controls.Diagram.Model
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Collections.Specialized;
	using System.ComponentModel;

	/// <summary>
	/// A headered node: a title bar (with a collapse button and an optional "show code" button), a few secondary
	/// header lines and a vertical stack of <see cref="DiagramRow"/> rows. Its height is computed by the canvas.
	/// </summary>
	public sealed class DiagramNode : DiagramItem
	{
		private static readonly PropertyChangedEventArgs RowsChangedArgs = new(nameof(Rows));

		private readonly HashSet<DiagramRow> _attachedRows = [];
		private String _name;
		private DiagramNodeKind _kind;
		private Boolean _isCollapsed;
		private Boolean _showCodeRows = true;
		private Boolean _showCodeButton = true;
		private Boolean _isEditing;

		/// <summary>
		/// Initializes a new node.
		/// </summary>
		/// <param name="name">Unique name of the node; it is the key used by <see cref="DiagramRow.LinkTargetName"/>.</param>
		public DiagramNode(String name)
		{
			this._name = name ?? String.Empty;
			this.Rows = [];
			this.HeaderLines = [];
			this.Rows.CollectionChanged += this.OnRowsCollectionChanged;
			this.HeaderLines.CollectionChanged += this.OnHeaderLinesChanged;
		}

		/// <summary>Gets or sets the node name shown in the title bar.</summary>
		public String Name
		{
			get => this._name;
			set => this.SetProperty(ref this._name, value ?? String.Empty);
		}

		/// <summary>Gets or sets the semantic kind that selects the colour scheme.</summary>
		public DiagramNodeKind Kind
		{
			get => this._kind;
			set => this.SetProperty(ref this._kind, value);
		}

		/// <summary>Gets or sets a value indicating whether only the header is shown.</summary>
		public Boolean IsCollapsed
		{
			get => this._isCollapsed;
			set => this.SetProperty(ref this._isCollapsed, value);
		}

		/// <summary>Gets or sets a value indicating whether rows of kind <see cref="DiagramRowKind.Code"/> are shown.</summary>
		public Boolean ShowCodeRows
		{
			get => this._showCodeRows;
			set => this.SetProperty(ref this._showCodeRows, value);
		}

		/// <summary>Gets or sets a value indicating whether the title bar shows the button that toggles <see cref="ShowCodeRows"/>.</summary>
		public Boolean ShowCodeButton
		{
			get => this._showCodeButton;
			set => this.SetProperty(ref this._showCodeButton, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="ShowCodeRows"/> was chosen explicitly for this node (as opposed to
		/// inheriting a global default). The canvas sets it when the user clicks the code button.
		/// </summary>
		public Boolean HasUserShowCodeSetting { get; set; }

		/// <summary>Gets or sets a value indicating whether the node is being edited elsewhere; it is drawn with an accent marker.</summary>
		public Boolean IsEditing
		{
			get => this._isEditing;
			set => this.SetProperty(ref this._isEditing, value);
		}

		/// <summary>Gets the secondary lines drawn under the title (for example the names of the nodes that link here).</summary>
		public ObservableCollection<String> HeaderLines { get; }

		/// <summary>Gets the rows of the node.</summary>
		public ObservableCollection<DiagramRow> Rows { get; }

		/// <inheritdoc />
		public override Double MinimumWidth => 100.0;

		/// <inheritdoc />
		public override Boolean HasAutomaticHeight => true;

		/// <summary>
		/// Creates a row, appends it to <see cref="Rows"/> and returns it.
		/// </summary>
		/// <param name="kind">Visual style of the row.</param>
		/// <param name="text">Displayed text.</param>
		/// <param name="detail">Optional collapsible detail line.</param>
		/// <param name="linkTargetName">Optional name of the node the row links to.</param>
		/// <returns>The new row.</returns>
		public DiagramRow AddRow(DiagramRowKind kind, String text, String? detail = null, String? linkTargetName = null)
		{
			DiagramRow row = new(kind, text, detail) { LinkTargetName = linkTargetName };
			this.Rows.Add(row);
			return row;
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return "\"" + this._name + "\"";
		}

		private void OnHeaderLinesChanged(Object? sender, NotifyCollectionChangedEventArgs e)
		{
			this.OnPropertyChanged(RowsChangedArgs);
		}

		private void OnRowsCollectionChanged(Object? sender, NotifyCollectionChangedEventArgs e)
		{
			HashSet<DiagramRow> current = new(this.Rows);
			List<DiagramRow> removed = [];
			foreach (DiagramRow row in this._attachedRows)
			{
				if (!current.Contains(row))
				{
					removed.Add(row);
				}
			}

			foreach (DiagramRow row in removed)
			{
				this._attachedRows.Remove(row);
				row.PropertyChanged -= this.OnRowPropertyChanged;
				if (ReferenceEquals(row.Owner, this))
				{
					row.Owner = null;
				}
			}

			foreach (DiagramRow row in current)
			{
				if (this._attachedRows.Add(row))
				{
					row.Owner = this;
					row.PropertyChanged += this.OnRowPropertyChanged;
				}
			}

			this.OnPropertyChanged(RowsChangedArgs);
		}

		private void OnRowPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			this.OnPropertyChanged(RowsChangedArgs);
		}
	}
}