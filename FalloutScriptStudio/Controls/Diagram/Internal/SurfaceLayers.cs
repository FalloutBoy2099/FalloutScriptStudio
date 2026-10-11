namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;

	/// <summary>
	/// The retained layers of the <see cref="DiagramSurface"/>, bottom to top: links, unselected items, selected items, overlay.
	/// </summary>
	[Flags]
	internal enum SurfaceLayers
	{
		None = 0,
		Lower = 1,
		Links = 2,
		Upper = 4,
		Overlay = 8,
		All = 15
	}
}
