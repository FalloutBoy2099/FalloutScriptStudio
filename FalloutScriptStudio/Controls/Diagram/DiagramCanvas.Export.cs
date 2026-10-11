namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.IO;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram.Internal;

	// Rendering the diagram to a bitmap or a PNG file.
	public sealed partial class DiagramCanvas
	{
		/// <summary>
		/// Renders the whole diagram (not just the visible part) to a bitmap, without selection decorations.
		/// </summary>
		/// <param name="scale">Pixels per world unit; reduced automatically so that no side exceeds 16384 pixels.</param>
		/// <param name="margin">Empty border around the diagram in world units.</param>
		/// <returns>A frozen bitmap.</returns>
		public BitmapSource RenderToBitmap(Double scale = 1.0, Double margin = 20.0)
		{
			return DiagramBitmapExporter.Render(this._scene, scale, margin);
		}

		/// <summary>
		/// Renders the whole diagram to a PNG stream.
		/// </summary>
		/// <param name="destination">The stream to write to.</param>
		/// <param name="scale">Pixels per world unit.</param>
		public void SaveToPng(Stream destination, Double scale = 1.0)
		{
			ArgumentNullException.ThrowIfNull(destination);
			PngBitmapEncoder encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(this.RenderToBitmap(scale)));
			encoder.Save(destination);
		}

		/// <summary>
		/// Renders the whole diagram to a PNG file.
		/// </summary>
		/// <param name="path">Destination file; an existing file is overwritten.</param>
		/// <param name="scale">Pixels per world unit.</param>
		public void SaveToPng(String path, Double scale = 1.0)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				this.SaveToPng(stream, scale);
			}
		}
	}
}
