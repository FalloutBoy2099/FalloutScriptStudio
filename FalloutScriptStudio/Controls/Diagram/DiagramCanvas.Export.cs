namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.IO;
	using System.Windows.Media.Imaging;
	using Core.Configuration;
	using Internal;

	// Rendering the diagram to a bitmap or a PNG file.
	public sealed partial class DiagramCanvas
	{
		/// <summary>
		/// Renders the whole diagram (not just the visible part) to a bitmap, without selection decorations.
		/// </summary>
		/// <param name="scale">Pixels per world unit; reduced automatically so that no side exceeds 16384 pixels and the image has at most 64 million pixels.</param>
		/// <param name="margin">Empty border around the diagram in world units.</param>
		/// <returns>A frozen bitmap.</returns>
		public BitmapSource RenderToBitmap(Double scale = 1.0, Double margin = 20.0)
		{
			return DiagramBitmapExporter.Render(this._scene, scale, margin);
		}

		/// <summary>
		/// Gets the size of the image <see cref="RenderToBitmap"/> would produce, without rendering it.
		/// </summary>
		/// <param name="scale">Pixels per world unit.</param>
		/// <param name="margin">Empty border around the diagram in world units.</param>
		/// <returns>The width and height in pixels, after the automatic reduction of a diagram that is too large.</returns>
		public (Int32 Width, Int32 Height) GetExportSize(Double scale = 1.0, Double margin = 20.0)
		{
			return DiagramBitmapExporter.GetSize(this._scene, scale, margin);
		}

		/// <summary>
		/// Renders the whole diagram to a PNG stream.
		/// </summary>
		/// <param name="destination">The stream to write to.</param>
		/// <param name="scale">Pixels per world unit.</param>
		public void SaveToPng(Stream destination, Double scale = 1.0)
		{
			ArgumentNullException.ThrowIfNull(destination);
			PngBitmapEncoder encoder = new();
			encoder.Frames.Add(BitmapFrame.Create(this.RenderToBitmap(scale)));
			encoder.Save(destination);
		}

		/// <summary>
		/// Renders the whole diagram to a PNG file.
		/// </summary>
		/// <remarks>
		/// The image is rendered and encoded first, then written through a temporary file, so a failing render never truncates an
		/// existing file.
		/// </remarks>
		/// <param name="path">Destination file; an existing file is overwritten.</param>
		/// <param name="scale">Pixels per world unit.</param>
		public void SaveToPng(String path, Double scale = 1.0)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			using (MemoryStream buffer = new())
			{
				this.SaveToPng(buffer, scale);
				AtomicFile.WriteAllBytes(path, buffer.ToArray());
			}
		}
	}
}