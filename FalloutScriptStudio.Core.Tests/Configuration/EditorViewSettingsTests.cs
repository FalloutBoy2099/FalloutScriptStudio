namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using FalloutScriptStudio.Core.Configuration;
	using Xunit;

	public sealed class EditorViewSettingsTests
	{
		[Fact]
		public void Defaults_MatchTheLegacyEditor()
		{
			EditorSettings editor = new EditorSettings();

			Assert.Equal(100, editor.ZoomPercent);
			Assert.False(editor.FormatCodeOnOpen);
		}

		[Fact]
		public void Clone_KeepsZoomAndFormatOnOpen()
		{
			AppSettings settings = new AppSettings();
			settings.Editor.ZoomPercent = 170;
			settings.Editor.FormatCodeOnOpen = true;

			AppSettings copy = settings.Clone();

			Assert.Equal(170, copy.Editor.ZoomPercent);
			Assert.True(copy.Editor.FormatCodeOnOpen);
		}

		[Theory]
		[InlineData(0, EditorSettings.MinZoomPercent)]
		[InlineData(39, EditorSettings.MinZoomPercent)]
		[InlineData(125, 125)]
		[InlineData(9999, EditorSettings.MaxZoomPercent)]
		public void Normalize_ClampsTheZoom(Int32 stored, Int32 expected)
		{
			AppSettings settings = new AppSettings();
			settings.Editor.ZoomPercent = stored;

			settings.Normalize();

			Assert.Equal(expected, settings.Editor.ZoomPercent);
		}
	}
}