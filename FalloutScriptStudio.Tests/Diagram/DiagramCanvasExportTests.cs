namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using static FalloutScriptStudio.Tests.Diagram.DiagramCanvasTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Diagram.DiagramCanvasTests")]
	public sealed class DiagramCanvasExportTests
	{
		[Fact]
		public void RenderToBitmap_ProducesDrawnImage()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("node001", 20, 20));
				canvas.AddItem(new DiagramNote { Text = "note", X = 400, Y = 40 });

				BitmapSource bitmap = canvas.RenderToBitmap(1.0, 10.0);

				Assert.True(bitmap.PixelWidth > 400);
				Assert.True(bitmap.PixelHeight > 80);
				Byte[] pixels = new Byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
				bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
				Boolean differs = false;
				for (Int32 i = 4; i < pixels.Length && !differs; i += 4)
				{
					differs = pixels[i] != pixels[0] || pixels[i + 1] != pixels[1] || pixels[i + 2] != pixels[2];
				}

				Assert.True(differs);
			});
		}

		[Fact]
		public void SaveToPng_WritesPngSignature()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("node001", 20, 20));
				using (MemoryStream stream = new MemoryStream())
				{
					canvas.SaveToPng(stream);

					Byte[] data = stream.ToArray();
					Assert.True(data.Length > 100);
					Assert.Equal(new Byte[] { 0x89, 0x50, 0x4E, 0x47 }, data.Take(4).ToArray());
				}
			});
		}

		[Fact]
		public void GetExportSize_OfAHugeDiagram_StaysWithinThePixelBudget()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("far-left", 0, 0));
				canvas.AddItem(CreateNode("far-right", 40000, 40000));

				(Int32 width, Int32 height) = canvas.GetExportSize();

				Assert.InRange(width, 1, 16384);
				Assert.InRange(height, 1, 16384);
				Assert.True((Int64)width * height <= 64L * 1000 * 1000, $"{width}x{height} pixels is more than the 32-bit process can allocate");
			});
		}

		[Fact]
		public void GetExportSize_OfASmallDiagram_IsNotReduced()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("node001", 20, 20));

				(Int32 width, Int32 height) = canvas.GetExportSize();
				BitmapSource bitmap = canvas.RenderToBitmap();

				Assert.Equal((bitmap.PixelWidth, bitmap.PixelHeight), (width, height));
			});
		}

		[Fact]
		public void SaveToPng_ToAFile_ReplacesTheExistingFileAndLeavesNoTemporaryFile()
		{
			StaTestRunner.Run(() =>
			{
				String folder = TestFiles.CreateWorkFolder("png");
				Directory.CreateDirectory(folder);
				try
				{
					String path = Path.Combine(folder, "chart.png");
					File.WriteAllText(path, "old");
					DiagramCanvas canvas = new DiagramCanvas();
					canvas.AddItem(CreateNode("node001", 20, 20));

					canvas.SaveToPng(path);

					Assert.Equal(new Byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(path).Take(4).ToArray());
					Assert.Equal(new[] { path }, Directory.GetFiles(folder));
				}
				finally
				{
					TestFiles.DeleteWorkFolder(folder);
				}
			});
		}

		[Fact]
		public void RenderToBitmap_EmptyCanvas_StillReturnsImage()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();

				BitmapSource bitmap = canvas.RenderToBitmap();

				Assert.True(bitmap.PixelWidth > 0);
				Assert.True(bitmap.PixelHeight > 0);
			});
		}

		[Fact]
		public void ThemeFallback_WorksWithoutFluentResources()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas { LowDetail = true, HighQuality = false };
				canvas.AddItem(CreateNode("node001", 20, 20));

				BitmapSource bitmap = canvas.RenderToBitmap();

				Assert.True(bitmap.PixelWidth > 0);
			});
		}
	}
}
