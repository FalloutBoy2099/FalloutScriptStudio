namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;

	/// <summary>
	/// What changed in a <see cref="DiagramScene"/>.
	/// </summary>
	[Flags]
	internal enum SceneChange
	{
		None = 0,

		/// <summary>Only the look of an item changed (text, editing flag...).</summary>
		Appearance = 1,

		/// <summary>An item moved; connector routes and the content extent must be recomputed.</summary>
		Geometry = 2,

		/// <summary>The size or content of an item changed; its layout must be recomputed.</summary>
		Layout = 4,

		/// <summary>Items were added, removed, hidden, shown or reordered.</summary>
		Structure = 8,

		/// <summary>The selection changed.</summary>
		Selection = 16,

		/// <summary>The hovered item or row changed.</summary>
		Hover = 32,

		/// <summary>The render context (theme, fonts, detail level) was replaced.</summary>
		Context = 64
	}
}