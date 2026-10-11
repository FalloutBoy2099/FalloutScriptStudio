namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Rendering;

	/// <summary>
	/// Position of the resize handles of a selected item. Handles keep a constant on-screen size, so their world size depends on the zoom.
	/// </summary>
	internal static class GripGeometry
	{
		public static Boolean Has(DiagramItem item, DiagramHitPart part)
		{
			return part switch
			{
				DiagramHitPart.ResizeEast or DiagramHitPart.ResizeWest => item.CanResizeHorizontally,
				DiagramHitPart.ResizeNorth or DiagramHitPart.ResizeSouth => item.CanResizeVertically,
				_ => false
			};
		}

		public static Rect GetRect(Rect bounds, DiagramHitPart part, Double zoom)
		{
			Double longSide = DiagramMetrics.GripLongSidePixels / zoom;
			Double shortSide = DiagramMetrics.GripShortSidePixels / zoom;
			Double midX = bounds.Left + (bounds.Width / 2.0);
			Double midY = bounds.Top + (bounds.Height / 2.0);
			return part switch
			{
				DiagramHitPart.ResizeWest => new Rect(bounds.Left - (shortSide / 2.0), midY - (longSide / 2.0), shortSide, longSide),
				DiagramHitPart.ResizeEast => new Rect(bounds.Right - (shortSide / 2.0), midY - (longSide / 2.0), shortSide, longSide),
				DiagramHitPart.ResizeNorth => new Rect(midX - (longSide / 2.0), bounds.Top - (shortSide / 2.0), longSide, shortSide),
				_ => new Rect(midX - (longSide / 2.0), bounds.Bottom - (shortSide / 2.0), longSide, shortSide)
			};
		}

		public static Rect GetHitRect(Rect bounds, DiagramHitPart part, Double zoom)
		{
			Rect rect = GetRect(bounds, part, zoom);
			rect.Inflate(DiagramMetrics.GripHitPaddingPixels / zoom, DiagramMetrics.GripHitPaddingPixels / zoom);
			return rect;
		}
	}
}
