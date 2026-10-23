namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;

	/// <summary>The picture of a window: its pixels, a few statistics that tell whether it is blank, and the PNG file.</summary>
	internal sealed class WindowImage
	{
		internal WindowImage(Byte[] pixels, Int32 width, Int32 height)
		{
			this.Pixels = pixels;
			this.Width = width;
			this.Height = height;
			(this.DistinctColors, this.DominantShare) = Measure(pixels, width, height);
		}

		/// <summary>Gets the pixels (BGRA, rows from top to bottom).</summary>
		public Byte[] Pixels { get; }

		/// <summary>Gets the width in pixels.</summary>
		public Int32 Width { get; }

		/// <summary>Gets the height in pixels.</summary>
		public Int32 Height { get; }

		/// <summary>Gets the number of different colors among the sampled pixels.</summary>
		public Int32 DistinctColors { get; }

		/// <summary>Gets the share (0 to 1) of the sampled pixels that have the most common color.</summary>
		public Double DominantShare { get; }

		/// <summary>Gets a value indicating whether the picture shows (almost) nothing: one color, or the few colors of a window frame around an empty client area (a window that shows content has hundreds, with the anti-aliased text).</summary>
		public Boolean IsBlank => this.DistinctColors < 40 || this.DominantShare > 0.9995;

		/// <summary>Captures a window; a window that does not paint yet is captured again until it shows content.</summary>
		/// <param name="window">The window handle.</param>
		/// <param name="timeout">How long to wait for a picture that is not blank.</param>
		/// <returns>The picture (it may be blank when the window stayed empty), or <see langword="null"/> when the window cannot be captured.</returns>
		public static WindowImage? Capture(IntPtr window, TimeSpan timeout)
		{
			WindowImage? last = null;
			DateTime limit = DateTime.UtcNow + timeout;
			do
			{
				// WPF does not present a frame for a window that is outside of every monitor until the window is asked to print itself
				// (the answer to the plain request is a black client area): the plain request wakes it, the full-content request that
				// follows shows what DWM composed.
				NativeMethods.CaptureWindow(window, out _, out _, 0);
				Byte[]? pixels = NativeMethods.CaptureWindow(window, out Int32 width, out Int32 height);
				if (pixels is not null)
				{
					last = new WindowImage(pixels, width, height);
					if (!last.IsBlank)
					{
						return last;
					}
				}

				System.Threading.Thread.Sleep(150);
			}
			while (DateTime.UtcNow < limit);

			return last;
		}

		/// <summary>Counts the pixels of a rectangle whose color satisfies a test.</summary>
		/// <param name="left">The left edge of the rectangle in pixels.</param>
		/// <param name="top">The top edge.</param>
		/// <param name="width">The width.</param>
		/// <param name="height">The height.</param>
		/// <param name="test">The test of a color (red, green, blue).</param>
		/// <returns>The number of pixels.</returns>
		public Int32 Count(Int32 left, Int32 top, Int32 width, Int32 height, Func<Byte, Byte, Byte, Boolean> test)
		{
			Int32 count = 0;
			for (Int32 y = Math.Max(0, top); y < Math.Min(this.Height, top + height); y++)
			{
				for (Int32 x = Math.Max(0, left); x < Math.Min(this.Width, left + width); x++)
				{
					Int32 offset = ((y * this.Width) + x) * 4;
					if (test(this.Pixels[offset + 2], this.Pixels[offset + 1], this.Pixels[offset]))
					{
						count++;
					}
				}
			}

			return count;
		}

		/// <summary>Gets the mean brightness (0 to 255) of all pixels.</summary>
		/// <returns>The brightness.</returns>
		public Double MeanBrightness()
		{
			Double sum = 0;
			for (Int32 index = 0; index + 3 < this.Pixels.Length; index += 4)
			{
				sum += ((this.Pixels[index + 2] * 0.299) + (this.Pixels[index + 1] * 0.587) + (this.Pixels[index] * 0.114));
			}

			return sum / Math.Max(1, this.Pixels.Length / 4);
		}

		/// <summary>Writes the picture as a PNG file.</summary>
		/// <param name="path">The file.</param>
		public void Save(String path)
		{
			// The capture of a window has no meaningful alpha channel (a popup is captured with its transparent parts black): the picture is saved opaque.
			Byte[] opaque = (Byte[])this.Pixels.Clone();
			for (Int32 index = 3; index < opaque.Length; index += 4)
			{
				opaque[index] = 255;
			}

			BitmapSource source = BitmapSource.Create(this.Width, this.Height, 96, 96, PixelFormats.Bgra32, null, opaque, this.Width * 4);
			PngBitmapEncoder encoder = new();
			encoder.Frames.Add(BitmapFrame.Create(source));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			using FileStream stream = File.Create(path);
			encoder.Save(stream);
		}

		private static (Int32 Distinct, Double Dominant) Measure(Byte[] pixels, Int32 width, Int32 height)
		{
			Dictionary<Int32, Int32> counts = [];
			Int32 samples = 0;
			Int32 stepX = 2;
			Int32 stepY = 2;
			for (Int32 y = 0; y < height; y += stepY)
			{
				for (Int32 x = 0; x < width; x += stepX)
				{
					Int32 offset = ((y * width) + x) * 4;
					Int32 color = pixels[offset] | (pixels[offset + 1] << 8) | (pixels[offset + 2] << 16);
					counts[color] = counts.TryGetValue(color, out Int32 count) ? count + 1 : 1;
					samples++;
				}
			}

			Int32 dominant = 0;
			foreach (Int32 count in counts.Values)
			{
				dominant = Math.Max(dominant, count);
			}

			return (counts.Count, samples == 0 ? 1 : (Double)dominant / samples);
		}
	}
}