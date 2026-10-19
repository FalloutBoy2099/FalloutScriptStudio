namespace FalloutScriptStudio.Controls.Diagram.Model
{
	using System;

	/// <summary>
	/// A resolved connector between two nodes. Links are created by the canvas from <see cref="DiagramRow.LinkTargetName"/>
	/// and are read-only to callers.
	/// </summary>
	public sealed class DiagramLink
	{
		internal DiagramLink(DiagramNode source, DiagramRow sourceRow, DiagramNode target)
		{
			this.Source = source;
			this.SourceRow = sourceRow;
			this.Target = target;
		}

		/// <summary>Gets the node the line starts at.</summary>
		public DiagramNode Source { get; }

		/// <summary>Gets the row of <see cref="Source"/> that is the origin of the line.</summary>
		public DiagramRow SourceRow { get; }

		/// <summary>Gets the node the arrow points to.</summary>
		public DiagramNode Target { get; }

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Source.Name + " -> " + this.Target.Name;
		}
	}
}