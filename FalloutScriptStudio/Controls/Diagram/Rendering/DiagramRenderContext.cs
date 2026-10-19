namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;

	/// <summary>
	/// Everything that influences how items are measured and drawn, other than the items themselves.
	/// Replaced as a whole when the theme, the font, the DPI or the detail level changes.
	/// </summary>
	internal sealed class DiagramRenderContext
	{
		public DiagramRenderContext(DiagramPalette palette, DiagramTextStyles styles, Boolean lowDetail, Boolean highQuality)
		{
			this.Palette = palette;
			this.Styles = styles;
			this.LowDetail = lowDetail;
			this.HighQuality = highQuality;
		}

		public DiagramPalette Palette { get; }

		public DiagramTextStyles Styles { get; }

		public Boolean LowDetail { get; }

		public Boolean HighQuality { get; }
	}
}