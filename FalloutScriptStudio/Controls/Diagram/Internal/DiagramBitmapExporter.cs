namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using Rendering;

	/// <summary>Renders a whole scene (not just the visible part) to a bitmap.</summary>
	internal static class DiagramBitmapExporter
	{
		/// <summary>The largest side of the image in pixels.</summary>
		public const Int32 MaxSide = 16384;

		/// <summary>
		/// The largest number of pixels of the image. A pixel takes 4 bytes and the application is a 32-bit process, so a
		/// square of the largest side (a gigabyte) could never be allocated.
		/// </summary>
		public const Int64 MaxPixelCount = 64L * 1000 * 1000;

		/// <summary>Calculates the size of the image of a diagram: the requested scale, reduced so that the image stays within the limits.</summary>
		/// <param name="worldWidth">The width of the diagram with its margins in world units.</param>
		/// <param name="worldHeight">The height of the diagram with its margins in world units.</param>
		/// <param name="scale">Pixels per world unit.</param>
		/// <param name="actualScale">Receives the scale that is used.</param>
		/// <returns>The size of the image in pixels.</returns>
		public static (Int32 Width, Int32 Height) CalculateSize(Double worldWidth, Double worldHeight, Double scale, out Double actualScale)
		{
			Double s = Math.Min(scale, MaxSide / Math.Max(worldWidth, worldHeight));
			// The factor leaves room for the rounding up of both sides to whole pixels.
			s = Math.Min(s, 0.999 * Math.Sqrt(MaxPixelCount / (worldWidth * worldHeight)));
			actualScale = Math.Max(s, 1e-6);
			Int32 pixelWidth = (Int32)Math.Min(MaxSide, Math.Max(1, Math.Ceiling(worldWidth * actualScale)));
			Int32 pixelHeight = (Int32)Math.Min(MaxSide, Math.Max(1, Math.Ceiling(worldHeight * actualScale)));
			return (pixelWidth, pixelHeight);
		}

		/// <summary>Calculates the size of the image of a scene.</summary>
		/// <param name="scene">The scene.</param>
		/// <param name="scale">Pixels per world unit.</param>
		/// <param name="margin">Empty border around the diagram in world units.</param>
		/// <returns>The size of the image in pixels.</returns>
		public static (Int32 Width, Int32 Height) GetSize(DiagramScene scene, Double scale, Double margin)
		{
			scene.EnsureLayouts();
			Rect bounds = GetBounds(scene);
			return CalculateSize(bounds.Width + (2.0 * margin), bounds.Height + (2.0 * margin), scale, out _);
		}

		/// <summary>Renders the scene without selection decorations.</summary>
		/// <param name="scene">The scene.</param>
		/// <param name="scale">Pixels per world unit; reduced automatically so that no side exceeds <see cref="MaxSide"/> pixels and the image has at most <see cref="MaxPixelCount"/> pixels.</param>
		/// <param name="margin">Empty border around the diagram in world units.</param>
		/// <returns>A frozen bitmap.</returns>
		public static BitmapSource Render(DiagramScene scene, Double scale, Double margin)
		{
			scene.EnsureLayouts();
			Rect bounds = GetBounds(scene);
			(Int32 pixelWidth, Int32 pixelHeight) = CalculateSize(bounds.Width + (2.0 * margin), bounds.Height + (2.0 * margin), scale, out Double s);

			DrawingVisual visual = new();
			TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Ideal);
			DiagramRenderer renderer = new(scene);
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

			RenderTargetBitmap bitmap = new(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);
			bitmap.Render(visual);
			bitmap.Freeze();
			return bitmap;
		}

		private static Rect GetBounds(DiagramScene scene)
		{
			Rect bounds = scene.GetContentBounds();
			return bounds.IsEmpty ? new Rect(0, 0, 1, 1) : bounds;
		}
	}
}