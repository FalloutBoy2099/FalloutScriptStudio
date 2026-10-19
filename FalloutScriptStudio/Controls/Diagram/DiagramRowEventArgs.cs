namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using Model;

	/// <summary>
	/// Describes a click on a row of a node.
	/// </summary>
	public sealed class DiagramRowEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes the arguments.
		/// </summary>
		public DiagramRowEventArgs(DiagramNode node, DiagramRow row, DiagramHitPart part, ModifierKeys modifiers, MouseButton button, Point worldPosition, Rect viewportBounds, Boolean isTextTrimmed)
		{
			this.Node = node;
			this.Row = row;
			this.Part = part;
			this.Modifiers = modifiers;
			this.Button = button;
			this.WorldPosition = worldPosition;
			this.ViewportBounds = viewportBounds;
			this.IsTextTrimmed = isTextTrimmed;
		}

		/// <summary>Gets the node that owns the row.</summary>
		public DiagramNode Node { get; }

		/// <summary>Gets the clicked row.</summary>
		public DiagramRow Row { get; }

		/// <summary>Gets which part of the row was hit: <see cref="DiagramHitPart.Row"/>, <see cref="DiagramHitPart.RowToggle"/> or <see cref="DiagramHitPart.RowDetail"/>.</summary>
		public DiagramHitPart Part { get; }

		/// <summary>Gets the modifier keys held during the click (for example Shift for "quick edit").</summary>
		public ModifierKeys Modifiers { get; }

		/// <summary>Gets the mouse button that was released.</summary>
		public MouseButton Button { get; }

		/// <summary>Gets the position of the click in world coordinates.</summary>
		public Point WorldPosition { get; }

		/// <summary>Gets the header rectangle of the row relative to the canvas control; useful to place a popup next to the row.</summary>
		public Rect ViewportBounds { get; }

		/// <summary>Gets a value indicating whether the text of the row is cut with an ellipsis, i.e. a popup with the full text would be useful.</summary>
		public Boolean IsTextTrimmed { get; }
	}
}