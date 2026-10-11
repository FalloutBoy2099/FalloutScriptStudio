namespace FalloutScriptStudio.Controls.Diagram.Model
{
	/// <summary>
	/// Visual style of a <see cref="DiagramRow"/>.
	/// </summary>
	public enum DiagramRowKind
	{
		/// <summary>A plain, possibly wrapped, line of text.</summary>
		Text,

		/// <summary>A single line of code in a monospace font. Hidden when <see cref="DiagramNode.ShowCodeRows"/> is <c>false</c>.</summary>
		Code,

		/// <summary>A tinted "reply" message row (NPC speech).</summary>
		Reply,

		/// <summary>A tinted "option" message row (player choice).</summary>
		Option,

		/// <summary>A boxed monospace row describing a call to another node.</summary>
		Call
	}
}
