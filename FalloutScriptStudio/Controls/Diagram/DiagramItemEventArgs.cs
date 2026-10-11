namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using FalloutScriptStudio.Controls.Diagram.Model;

	/// <summary>
	/// Describes an item (and possibly a row of it) for hover, selection and double-click notifications.
	/// </summary>
	public sealed class DiagramItemEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes the arguments.
		/// </summary>
		/// <param name="item">The item concerned, or <c>null</c> when the pointer left all items.</param>
		/// <param name="row">The row under the pointer, if any.</param>
		public DiagramItemEventArgs(DiagramItem? item, DiagramRow? row = null)
		{
			this.Item = item;
			this.Row = row;
		}

		/// <summary>Gets the item concerned; <c>null</c> for "no item".</summary>
		public DiagramItem? Item { get; }

		/// <summary>Gets the row under the pointer, if the event concerns a row of a node.</summary>
		public DiagramRow? Row { get; }

		/// <summary>Gets the item as a node, or <c>null</c>.</summary>
		public DiagramNode? Node => this.Item as DiagramNode;
	}
}
