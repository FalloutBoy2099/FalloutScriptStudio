namespace FalloutScriptStudio.Controls.Diagram
{
	/// <summary>
	/// The part of an item that is under a point; see <see cref="DiagramHitTestResult"/>.
	/// </summary>
	public enum DiagramHitPart
	{
		/// <summary>Nothing is under the point.</summary>
		None,

		/// <summary>The body of an item (a note, or the empty area of a node).</summary>
		Body,

		/// <summary>The title bar of a node.</summary>
		Title,

		/// <summary>The collapse / expand button in the title bar.</summary>
		CollapseButton,

		/// <summary>The "show code rows" button in the title bar.</summary>
		CodeButton,

		/// <summary>The header of a row (its message text).</summary>
		Row,

		/// <summary>The expand / collapse chevron of a row.</summary>
		RowToggle,

		/// <summary>The expanded detail line of a row.</summary>
		RowDetail,

		/// <summary>The west (left) resize handle of a selected item.</summary>
		ResizeWest,

		/// <summary>The east (right) resize handle of a selected item.</summary>
		ResizeEast,

		/// <summary>The north (top) resize handle of a selected item.</summary>
		ResizeNorth,

		/// <summary>The south (bottom) resize handle of a selected item.</summary>
		ResizeSouth
	}
}
