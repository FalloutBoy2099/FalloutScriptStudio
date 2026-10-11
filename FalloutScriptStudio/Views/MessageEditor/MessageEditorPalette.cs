namespace FalloutScriptStudio.Views.MessageEditor
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>
	/// The background colors of comment lines and of the line number column for every highlight choice and theme.
	/// The light values are the colors of the legacy editor.
	/// </summary>
	internal static class MessageEditorPalette
	{
		/// <summary>
		/// Gets the colors for a highlight choice.
		/// </summary>
		/// <param name="color">The user's choice.</param>
		/// <param name="dark">Whether the dark theme is active.</param>
		/// <returns>The background of comment lines, the background of the line number column and the foreground of the line numbers.</returns>
		public static (Color Comment, Color LineColumn, Color LineForeground) Get(CommentHighlightColor color, Boolean dark)
		{
			if (dark)
			{
				Color lineForeground = Color.FromRgb(0xE0, 0xA0, 0x70);
				return color switch
				{
					CommentHighlightColor.PaleGreen => (Color.FromRgb(0x1F, 0x4A, 0x2A), Color.FromRgb(0x24, 0x3A, 0x2A), lineForeground),
					CommentHighlightColor.Lavender => (Color.FromRgb(0x3A, 0x3A, 0x5E), Color.FromRgb(0x30, 0x30, 0x48), lineForeground),
					_ => (Color.FromRgb(0x4B, 0x49, 0x1E), Color.FromRgb(0x3A, 0x39, 0x22), lineForeground),
				};
			}

			Color chocolate = Color.FromRgb(0xD2, 0x69, 0x1E);
			return color switch
			{
				CommentHighlightColor.PaleGreen => (Color.FromRgb(195, 255, 195), Color.FromRgb(215, 255, 215), chocolate),
				CommentHighlightColor.Lavender => (Color.FromRgb(0xE6, 0xE6, 0xFA), Color.FromRgb(240, 240, 250), chocolate),
				_ => (Color.FromRgb(255, 255, 200), Color.FromRgb(0xFF, 0xFF, 0xE0), chocolate),
			};
		}

		/// <summary>
		/// Publishes the brushes the window XAML uses (comment rows, line number column, search matches) for a highlight choice and theme.
		/// </summary>
		/// <param name="resources">The resources of the window.</param>
		/// <param name="color">The user's choice.</param>
		/// <param name="dark">Whether the dark theme is active.</param>
		public static void Apply(ResourceDictionary resources, CommentHighlightColor color, Boolean dark)
		{
			(Color comment, Color line, Color lineForeground) = Get(color, dark);
			resources["MessageCommentRowBrush"] = CreateBrush(comment);
			resources["MessageLineColumnBrush"] = CreateBrush(line);
			resources["MessageLineForegroundBrush"] = CreateBrush(lineForeground);
			resources["MessageSearchMatchBrush"] = CreateBrush(dark ? Color.FromArgb(0x50, 0xFF, 0xB9, 0x00) : Color.FromArgb(0x66, 0xFF, 0xD5, 0x4F));
		}

		/// <summary>Creates a frozen brush.</summary>
		/// <param name="color">The color of the brush.</param>
		public static SolidColorBrush CreateBrush(Color color)
		{
			SolidColorBrush brush = new SolidColorBrush(color);
			brush.Freeze();
			return brush;
		}
	}
}
