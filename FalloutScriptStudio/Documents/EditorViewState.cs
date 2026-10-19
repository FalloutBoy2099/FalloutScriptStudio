namespace FalloutScriptStudio.Documents
{
	using System;
	using System.Collections.Generic;
	using Editor.Navigation;

	/// <summary>
	/// The view state of the editor of one document (caret, selection, scroll position, folded regions, Back / Forward
	/// history…). It lives on the <see cref="DocumentViewModel"/> so that it survives when the editor view is destroyed and
	/// created again, for example by tab virtualization. The editor view reads it when it is loaded and writes it when it is unloaded.
	/// </summary>
	public sealed class EditorViewState
	{
		/// <summary>Gets or sets a value indicating whether an editor view has been shown for the document at least once.</summary>
		public Boolean HasBeenShown { get; set; }

		/// <summary>Gets or sets the caret offset.</summary>
		public Int32 CaretOffset { get; set; }

		/// <summary>Gets or sets the 1-based caret line (updated while the view is shown, so it is valid even after the view was unloaded).</summary>
		public Int32 CaretLine { get; set; } = 1;

		/// <summary>Gets or sets the offset of the selection start.</summary>
		public Int32 SelectionStart { get; set; }

		/// <summary>Gets or sets the selection length (0 for none).</summary>
		public Int32 SelectionLength { get; set; }

		/// <summary>Gets or sets the vertical scroll offset in device independent pixels.</summary>
		public Double VerticalOffset { get; set; }

		/// <summary>Gets or sets the horizontal scroll offset in device independent pixels.</summary>
		public Double HorizontalOffset { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the document is shown in two panes. Like in the legacy editor (one text control per tab)
		/// the split stays when another tab is selected and the view is created again.
		/// </summary>
		public Boolean IsSplit { get; set; }

		/// <summary>Gets or sets the share of the height the first pane gets while the document is split (0 to 1).</summary>
		public Double SplitRatio { get; set; } = 0.5;

		/// <summary>Gets or sets the caret offset of the second pane.</summary>
		public Int32 SecondaryCaretOffset { get; set; }

		/// <summary>Gets or sets the vertical scroll offset of the second pane, or a negative value to start where the first pane is.</summary>
		public Double SecondaryVerticalOffset { get; set; } = -1;

		/// <summary>
		/// Gets or sets the keys of the folded regions as stored by the folding controller, or <see langword="null"/> when
		/// the state was never captured (then the remembered collapsed procedures of the script are used).
		/// </summary>
		public ISet<String>? FoldedRegions { get; set; }

		/// <summary>Gets or sets the text whose occurrences are highlighted in the editor, or <see langword="null"/>.</summary>
		public String? HighlightedText { get; set; }

		/// <summary>Gets the Back / Forward caret history.</summary>
		public CaretHistory History { get; } = new CaretHistory();

		/// <summary>
		/// Gets or sets a navigation request (1-based line, 0 for none) made while no view was shown; the view performs
		/// it when it is loaded.
		/// </summary>
		public Int32 PendingLine { get; set; }

		/// <summary>Gets or sets the 1-based column of the pending navigation, or -1.</summary>
		public Int32 PendingColumn { get; set; } = -1;

		/// <summary>Gets or sets the number of characters to select after the pending navigation, or -1.</summary>
		public Int32 PendingSelectLength { get; set; } = -1;

		/// <summary>Stores a navigation that the view performs when it is shown.</summary>
		/// <param name="line">1-based line.</param>
		/// <param name="column">1-based column or -1.</param>
		/// <param name="selectLength">Number of characters to select or -1.</param>
		public void RequestGoTo(Int32 line, Int32 column = -1, Int32 selectLength = -1)
		{
			this.PendingLine = line;
			this.PendingColumn = column;
			this.PendingSelectLength = selectLength;
		}
	}
}