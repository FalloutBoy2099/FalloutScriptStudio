namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram.Rendering;

	/// <summary>Renders a whole scene (not just the visible part) to a bitmap.</summary>
	internal static class DiagramBitmapExporter
	{
		/// <summary>Renders the scene without selection decorations.</summary>
		/// <param name="scene">The scene.</param>
		/// <param name="scale">Pixels per world unit; reduced automatically so that no side exceeds 16384 pixels.</param>
		/// <param name="margin">Empty border around the diagram in world units.</param>
		/// <returns>A frozen bitmap.</returns>
		public static BitmapSource Render(DiagramScene scene, Double scale, Double margin)
		{
			scene.EnsureLayouts();
			Rect bounds = scene.GetContentBounds();
			if (bounds.IsEmpty)
			{
				bounds = new Rect(0, 0, 1, 1);
			}

			Double worldWidth = bounds.Width + (2.0 * margin);
			Double worldHeight = bounds.Height + (2.0 * margin);
			Double s = Math.Max(0.01, Math.Min(scale, 16384.0 / Math.Max(worldWidth, worldHeight)));
			Int32 pixelWidth = Math.Max(1, (Int32)Math.Ceiling(worldWidth * s));
			Int32 pixelHeight = Math.Max(1, (Int32)Math.Ceiling(worldHeight * s));

			DrawingVisual visual = new DrawingVisual();
			TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Ideal);
			DiagramRenderer renderer = new DiagramRenderer(scene);
			using (DrawingContext dc = visual.RenderOpen())
			{
				dc.DrawRectangle(scene.Context.Palette.Background, null, new Rect(0, 0, pixelWidth, pixelHeight));
				dc.PushTransform(new ScaleTransform(s, s));
				dc.PushTransform(new TranslateTransform(margin - bounds.X, margin - bounds.Y));
				renderer.DrawLinks(dc, s, false, false);
				renderer.DrawItems(dc, false, false);
				dc.Pop();
				dc.Pop();
			}

			RenderTargetBitmap bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);
			bitmap.Render(visual);
			bitmap.Freeze();
			return bitmap;
		}
	}
}
