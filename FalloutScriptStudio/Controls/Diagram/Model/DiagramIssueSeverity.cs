namespace FalloutScriptStudio.Controls.Diagram.Model
{
	/// <summary>How serious the problem is that a <see cref="DiagramNode"/> is marked with. The canvas draws a badge whose shape (a cross, an exclamation mark or an "i") tells it, not only its colour.</summary>
	public enum DiagramIssueSeverity
	{
		/// <summary>No problem: nothing is drawn.</summary>
		None = 0,

		/// <summary>A note: an "i" badge.</summary>
		Info,

		/// <summary>A probable mistake: an exclamation mark badge.</summary>
		Warning,

		/// <summary>A mistake: a cross badge.</summary>
		Error,
	}
}