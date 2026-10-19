namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Remembered window layout.
	/// </summary>
	public sealed class WindowSettings
	{
		/// <summary>Gets or sets the remembered placement of each window.</summary>
		public Dictionary<WindowKind, WindowPlacement> Placements { get; set; } = [];

		/// <summary>Gets or sets the position of the splitter between the editor and the log panels, or -1 for the default.</summary>
		public Int32 SplitterPosition { get; set; } = -1;

		/// <summary>Gets or sets the position of the second splitter, or -1 for the default.</summary>
		public Int32 SecondarySplitterPosition { get; set; } = -1;

		/// <summary>Gets or sets a value indicating whether the log panel is shown.</summary>
		public Boolean ShowLog { get; set; } = true;

		/// <summary>
		/// Gets the placement of a window.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <returns>The placement, or <see langword="null"/> if none was stored.</returns>
		public WindowPlacement? GetPlacement(WindowKind window)
		{
			return this.Placements.TryGetValue(window, out WindowPlacement? placement) && !placement.IsEmpty ? placement : null;
		}

		/// <summary>
		/// Stores the placement of a window.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="placement">The placement.</param>
		public void SetPlacement(WindowKind window, WindowPlacement placement)
		{
			ArgumentNullException.ThrowIfNull(placement);
			this.Placements[window] = placement;
		}
	}
}