namespace FalloutScriptStudio.Editor.Completion
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Editor.Tips;
	using FalloutScriptStudio.ViewModels.Editor;
	using FalloutScriptStudio.Views.Editor;
	using ICSharpCode.AvalonEdit.CodeCompletion;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;
	using Wpf.Ui.Controls;

	/// <summary>One entry of the completion list: an opcode, procedure, variable or macro.</summary>
	public sealed class ScriptCompletionData : ICompletionData
	{
		private readonly AutosuggestItem _item;
		private readonly CompletionAppearance _appearance;
		private readonly Snippet? _snippet;
		private readonly Action<Snippet, ISegment>? _insertSnippet;

		public ScriptCompletionData(AutosuggestItem item, CompletionAppearance appearance)
			: this(item, appearance, null, null)
		{
		}

		/// <summary>Initializes the entry of a snippet: choosing it expands the snippet instead of inserting its trigger.</summary>
		/// <param name="item">The entry.</param>
		/// <param name="appearance">How the list looks.</param>
		/// <param name="snippet">The snippet, for an entry of the kind <see cref="AutosuggestKind.Snippet"/>.</param>
		/// <param name="insertSnippet">Expands the snippet in the place of the typed word.</param>
		public ScriptCompletionData(AutosuggestItem item, CompletionAppearance appearance, Snippet? snippet, Action<Snippet, ISegment>? insertSnippet)
		{
			this._item = item;
			this._appearance = appearance;
			this._snippet = snippet;
			this._insertSnippet = insertSnippet;
			this.Symbol = GetSymbol(item.Kind);
			this.ItemBrush = GetBrush(item.Kind, appearance);
			this.ItemFontWeight = appearance.Colored ? FontWeights.SemiBold : FontWeights.Normal;
		}

		/// <summary>Gets the kind of the entry.</summary>
		public AutosuggestKind Kind => this._item.Kind;

		/// <summary>Gets the Fluent icon of the entry kind.</summary>
		public SymbolRegular Symbol { get; }

		/// <summary>Gets the brush of the icon and the text.</summary>
		public Brush ItemBrush { get; }

		/// <summary>Gets the weight of the text.</summary>
		public FontWeight ItemFontWeight { get; }

		/// <summary>Gets the font of the text.</summary>
		public FontFamily ItemFontFamily => this._appearance.FontFamily;

		/// <inheritdoc />
		public ImageSource? Image => null;

		/// <inheritdoc />
		public String Text => this._item.Name;

		/// <inheritdoc />
		public Object Content => this._item.Name;

		/// <inheritdoc />
		public Object? Description
		{
			get
			{
				if (String.IsNullOrWhiteSpace(this._item.Description))
				{
					return null;
				}

				TipKind kind = this._item.Kind switch
				{
					AutosuggestKind.Opcode => TipKind.Opcode,
					AutosuggestKind.Procedure => TipKind.Procedure,
					AutosuggestKind.Variable => TipKind.Variable,
					_ => TipKind.Macro,
				};
				TipInfo tip = TipInfo.FromDescription(this._item.Description, kind);
				return new EditorTipView(EditorTipViewModel.Create(new[] { tip }, this._appearance.Palette, this._appearance.FontFamily, this._appearance.FontSize));
			}
		}

		/// <inheritdoc />
		public Double Priority => 0;

		/// <inheritdoc />
		public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
		{
			if (this._snippet is not null && this._insertSnippet is not null)
			{
				this._insertSnippet(this._snippet, completionSegment);
				return;
			}

			textArea.Document.Replace(completionSegment, this._item.Name);
		}

		private static SymbolRegular GetSymbol(AutosuggestKind kind)
		{
			return kind switch
			{
				AutosuggestKind.Opcode => SymbolRegular.Flash24,
				AutosuggestKind.Procedure => SymbolRegular.Cube24,
				AutosuggestKind.Variable => SymbolRegular.BracesVariable24,
				AutosuggestKind.Snippet => SymbolRegular.CodeBlock24,
				_ => SymbolRegular.NumberSymbol24,
			};
		}

		private static Brush GetBrush(AutosuggestKind kind, CompletionAppearance appearance)
		{
			if (!appearance.Colored)
			{
				return appearance.Palette.Foreground;
			}

			String color = appearance.Palette.IsDark
				? kind switch
				{
					AutosuggestKind.Opcode => "Gold",
					AutosuggestKind.Procedure => "Violet",
					AutosuggestKind.Variable => "LightSkyBlue",
					AutosuggestKind.Snippet => "LightGreen",
					_ => "AliceBlue",
				}
				: kind switch
				{
					AutosuggestKind.Opcode => "DarkMagenta",
					AutosuggestKind.Procedure => "MediumVioletRed",
					AutosuggestKind.Variable => "RoyalBlue",
					AutosuggestKind.Snippet => "SeaGreen",
					_ => "Indigo",
				};
			SolidColorBrush brush = new(EditorPalette.ParseColor(color));
			brush.Freeze();
			return brush;
		}
	}
}