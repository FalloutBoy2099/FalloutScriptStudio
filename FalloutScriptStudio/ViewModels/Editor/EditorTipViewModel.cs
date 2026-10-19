namespace FalloutScriptStudio.ViewModels.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Media;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Tips;

	/// <summary>The content of an editor tooltip (hover tip, code tip or completion item description).</summary>
	public sealed class EditorTipViewModel
	{
		public EditorTipViewModel(IReadOnlyList<TipSectionViewModel> sections, FontFamily titleFontFamily, Double titleFontSize)
		{
			this.Sections = sections;
			this.TitleFontFamily = titleFontFamily;
			this.TitleFontSize = titleFontSize;
		}

		/// <summary>Gets the blocks shown one below the other.</summary>
		public IReadOnlyList<TipSectionViewModel> Sections { get; }

		/// <summary>Gets the font of the title lines (the editor font).</summary>
		public FontFamily TitleFontFamily { get; }

		/// <summary>Gets the size of the title font in device independent pixels.</summary>
		public Double TitleFontSize { get; }

		/// <summary>Creates the view model of tips.</summary>
		/// <param name="tips">The tips to show.</param>
		/// <param name="palette">The colors of the current theme.</param>
		/// <param name="editorFont">The editor font family.</param>
		/// <param name="editorFontSize">The editor font size in device independent pixels.</param>
		/// <returns>The view model.</returns>
		public static EditorTipViewModel Create(IEnumerable<TipInfo> tips, EditorPalette palette, FontFamily editorFont, Double editorFontSize)
		{
			List<TipSectionViewModel> sections = tips
				.Select(tip => new TipSectionViewModel(tip.Title, tip.Body, GetTitleBrush(tip.Kind, palette)))
				.ToList();
			return new EditorTipViewModel(sections, editorFont, Math.Clamp(editorFontSize, 11, 16));
		}

		private static Brush GetTitleBrush(TipKind kind, EditorPalette palette)
		{
			return kind switch
			{
				TipKind.Opcode => palette.GetTokenBrush("FalloutBaseFunctions"),
				TipKind.Procedure => palette.ProcedureName,
				TipKind.Macro => palette.GetTokenBrush("UserMacros"),
				TipKind.Message => palette.GetTokenBrush("String"),
				TipKind.Include => palette.GetTokenBrush("IncludePath"),
				TipKind.Error => palette.ErrorUnderline,
				TipKind.Warning => palette.WarningUnderline,
				_ => palette.Foreground,
			};
		}
	}
}