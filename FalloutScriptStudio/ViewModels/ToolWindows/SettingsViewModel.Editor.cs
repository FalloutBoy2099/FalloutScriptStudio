namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;

	// Code editor page: tab size, font size, file encoding.
	public sealed partial class SettingsViewModel
	{
		/// <summary>Gets the file encodings to choose from.</summary>
		public IReadOnlyList<Choice<TextEncodingKind>> EncodingChoices { get; } = new List<Choice<TextEncodingKind>>
		{
			new(TextEncodingKind.Ansi, Loc.Get("Str.ToolWindows.Choice.Encoding.Ansi")),
			new(TextEncodingKind.Oem866, Loc.Get("Str.ToolWindows.Choice.Encoding.Oem866")),
		};

		/// <summary>Gets the levels of the semantic analysis of the scripts to choose from.</summary>
		public IReadOnlyList<Choice<LiveDiagnosticsLevel>> LiveDiagnosticsChoices { get; } = new List<Choice<LiveDiagnosticsLevel>>
		{
			new(LiveDiagnosticsLevel.Off, Loc.Get("Str.ToolWindows.Choice.Diagnostics.Off")),
			new(LiveDiagnosticsLevel.ErrorsOnly, Loc.Get("Str.ToolWindows.Choice.Diagnostics.ErrorsOnly")),
			new(LiveDiagnosticsLevel.ErrorsAndWarnings, Loc.Get("Str.ToolWindows.Choice.Diagnostics.ErrorsAndWarnings")),
		};

		/// <summary>
		/// Gets or sets the ignore list of the findings about global variables as text, one entry per line (a name, a macro, a number, or a
		/// prefix that ends with <c>*</c>).
		/// </summary>
		public String GlobalVariableIgnoreText
		{
			get => String.Join(Environment.NewLine, this.Draft.Editor.GlobalVariableIgnore);
			set
			{
				List<String> entries = (value ?? String.Empty)
					.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (!entries.SequenceEqual(this.Draft.Editor.GlobalVariableIgnore, StringComparer.Ordinal))
				{
					this.Draft.Editor.GlobalVariableIgnore = entries;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>Gets the smallest tab size.</summary>
		public Double MinTabSize => EditorSettings.MinTabSize;

		/// <summary>Gets the largest tab size.</summary>
		public Double MaxTabSize => EditorSettings.MaxTabSize;

		/// <summary>Gets the smallest font size adjustment.</summary>
		public Double MinFontSizeAdjustment => -6;

		/// <summary>Gets the largest font size adjustment.</summary>
		public Double MaxFontSizeAdjustment => 40;

		/// <summary>Gets or sets the tab size (the value is limited to the allowed range).</summary>
		public Double? TabSize
		{
			get => this.Draft.Editor.TabSize;
			set
			{
				if (value.HasValue && !Double.IsNaN(value.Value))
				{
					Int32 size = (Int32)Math.Clamp(Math.Round(value.Value), EditorSettings.MinTabSize, EditorSettings.MaxTabSize);
					if (size != this.Draft.Editor.TabSize)
					{
						this.Draft.Editor.TabSize = size;
						this.OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>Gets or sets the size adjustment of the editor font in points (the value is limited to the allowed range).</summary>
		public Double? FontSizeAdjustment
		{
			get => this.Draft.Editor.FontSizeAdjustment;
			set
			{
				if (value.HasValue && !Double.IsNaN(value.Value))
				{
					Int32 size = (Int32)Math.Clamp(Math.Round(value.Value), this.MinFontSizeAdjustment, this.MaxFontSizeAdjustment);
					if (size != this.Draft.Editor.FontSizeAdjustment)
					{
						this.Draft.Editor.FontSizeAdjustment = size;
						this.OnPropertyChanged();
					}
				}
			}
		}
	}
}