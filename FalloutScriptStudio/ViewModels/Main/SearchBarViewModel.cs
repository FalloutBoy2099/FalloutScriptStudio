namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;

	/// <summary>The quick search / replace bar above the editor (legacy <c>SearchToolStrip</c>).</summary>
	public sealed partial class SearchBarViewModel : ObservableObject
	{
		private const Int32 MaxHighlightedMatches = 2000;

		private readonly IWorkspace _workspace;
		private readonly ISettingsService _settings;
		private readonly ISoundService _sounds;
		private readonly ITextSearcher _searcher;
		private Boolean _loading;

		/// <summary>Initializes the search bar.</summary>
		public SearchBarViewModel(IWorkspace workspace, ISettingsService settings, ISoundService sounds, ITextSearcher searcher)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(sounds);
			ArgumentNullException.ThrowIfNull(searcher);
			this._workspace = workspace;
			this._settings = settings;
			this._sounds = sounds;
			this._searcher = searcher;

			this._loading = true;
			this.MatchCase = !settings.Settings.Editor.SearchIgnoreCase;
			this.WholeWord = settings.Settings.Editor.SearchWholeWord;
			this._loading = false;
			foreach (String query in settings.SearchHistory.Queries)
			{
				this.History.Add(query);
			}

			this._workspace.ActiveDocumentChanged += (_, _) =>
			{
				if (this._workspace.ActiveDocument is null)
				{
					this.IsVisible = false;
				}
				else
				{
					this.UpdateHighlights();
				}
			};
		}

		/// <summary>Raised when the search text box should take the keyboard focus.</summary>
		public event EventHandler? FocusRequested;

		/// <summary>Gets the previous search texts, newest first.</summary>
		public ObservableCollection<String> History { get; } = new ObservableCollection<String>();

		/// <summary>True when the bar is shown.</summary>
		[ObservableProperty]
		public partial Boolean IsVisible { get; set; }

		/// <summary>The text to find.</summary>
		[ObservableProperty]
		public partial String SearchText { get; set; } = String.Empty;

		/// <summary>The replacement text.</summary>
		[ObservableProperty]
		public partial String ReplaceText { get; set; } = String.Empty;

		/// <summary>True for a case-sensitive search.</summary>
		[ObservableProperty]
		public partial Boolean MatchCase { get; set; }

		/// <summary>True to find whole words only.</summary>
		[ObservableProperty]
		public partial Boolean WholeWord { get; set; }

		/// <summary>True when the search text is a regular expression.</summary>
		[ObservableProperty]
		public partial Boolean UseRegex { get; set; }

		/// <summary>The result of the last operation ("Not found", "3 replaced"…).</summary>
		[ObservableProperty]
		public partial String StatusText { get; set; } = String.Empty;

		private SearchQuery Query => new SearchQuery(this.SearchText)
		{
			MatchCase = this.MatchCase,
			WholeWord = this.WholeWord,
			UseRegex = this.UseRegex,
		};

		/// <summary>Shows the bar, optionally filled with the word at the caret.</summary>
		public void Show(Boolean takeWordFromEditor)
		{
			if (this._workspace.ActiveDocument is null)
			{
				return;
			}

			if (takeWordFromEditor)
			{
				this.UseSelection();
			}

			this.IsVisible = true;
			this.UpdateHighlights();
			this.FocusRequested?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Takes the selected text (or the word at the caret) as the search text.</summary>
		[RelayCommand]
		public void UseSelection()
		{
			String word = this._workspace.ActiveDocument?.Editor?.WordAtCaret ?? String.Empty;
			if (word.Length > 0 && word.IndexOf('\n') < 0)
			{
				this.SearchText = word;
			}
		}

		/// <summary>Hides the bar and gives the focus back to the editor.</summary>
		[RelayCommand]
		public void Hide()
		{
			this.IsVisible = false;
			IEditorSurface? editor = this._workspace.ActiveDocument?.Editor;
			editor?.ClearSearchMatches();
			editor?.FocusEditor();
		}

		/// <summary>Finds the next match after the selection, wrapping at the end.</summary>
		[RelayCommand]
		public void FindNext()
		{
			if (!this.TryGetContext(out DocumentViewModel? document, out IEditorSurface? editor) || document is null || editor is null)
			{
				return;
			}

			Int32 start = editor.SelectionLength > 0 ? editor.SelectionStart + 1 : editor.CaretOffset;
			TextSpan? span = this._searcher.FindNext(document.TextDocument.Text, this.Query, start, true);
			this.Select(editor, span, span is { } found && found.Offset < start ? Loc.Get("Str.Shell.Search.WrappedToStart") : String.Empty);
			this.RememberQuery();
		}

		/// <summary>Finds the previous match before the selection, wrapping at the beginning.</summary>
		[RelayCommand]
		public void FindPrevious()
		{
			if (!this.TryGetContext(out DocumentViewModel? document, out IEditorSurface? editor) || document is null || editor is null)
			{
				return;
			}

			Int32 end = editor.SelectionLength > 0 ? editor.SelectionStart : editor.CaretOffset;
			TextSpan? span = this._searcher.FindPrevious(document.TextDocument.Text, this.Query, end, true);
			this.Select(editor, span, span is { } found && found.End > end ? Loc.Get("Str.Shell.Search.WrappedToEnd") : String.Empty);
			this.RememberQuery();
		}

		/// <summary>Replaces the current match (when the selection is one) and moves to the next one.</summary>
		[RelayCommand]
		public void Replace()
		{
			if (!this.TryGetContext(out DocumentViewModel? document, out IEditorSurface? editor) || document is null || editor is null)
			{
				return;
			}

			if (editor.SelectionLength > 0)
			{
				TextEdit? edit = this._searcher.ReplaceNext(document.TextDocument.Text, this.Query, this.ReplaceText, editor.SelectionStart, false);
				if (edit is not null && edit.Offset == editor.SelectionStart && edit.Length == editor.SelectionLength)
				{
					editor.ReplaceSelection(edit.NewText);
				}
			}

			this.FindNext();
		}

		/// <summary>Replaces every match in the document as one undoable step.</summary>
		[RelayCommand]
		public void ReplaceAll()
		{
			if (!this.TryGetContext(out DocumentViewModel? document, out IEditorSurface? _) || document is null)
			{
				return;
			}

			ReplaceResult result = this._searcher.ReplaceAll(document.TextDocument.Text, this.Query, this.ReplaceText);
			if (result.Count == 0)
			{
				this._sounds.PlayNotFound();
				this.StatusText = Loc.Get("Str.Shell.Search.NotFound");
				return;
			}

			document.TextDocument.ApplyEdits(result.Edits);
			this.StatusText = Loc.Format("Str.Shell.Search.Replaced", result.Count);
			this.RememberQuery();
			this.UpdateHighlights();
		}

		/// <summary>Lists every matching line of the document in the Errors tab.</summary>
		[RelayCommand]
		public void FindAll()
		{
			if (!this.TryGetContext(out DocumentViewModel? document, out IEditorSurface? _) || document is null)
			{
				return;
			}

			IReadOnlyList<SearchResultRow> rows = this._searcher.FindAllLines(document.TextDocument.Text, document.FilePath ?? BuildError.NoFile, this.Query);
			if (rows.Count == 0)
			{
				this._sounds.PlayNotFound();
				this.StatusText = Loc.Get("Str.Shell.Search.NotFound");
				return;
			}

			this.StatusText = Loc.Format("Str.Shell.Search.Lines", rows.Count);
			this.RememberQuery();
			this._workspace.ShowSearchResults(rows.Select(row => new BuildError(BuildErrorType.Search, row.LineText, row.File, row.Line, row.Column, row.Length)).ToList());
		}

		partial void OnSearchTextChanged(String value)
		{
			this.StatusText = String.Empty;
			this.UpdateHighlights();
		}

		partial void OnMatchCaseChanged(Boolean value)
		{
			if (!this._loading)
			{
				this._settings.Update(settings => settings.Editor.SearchIgnoreCase = !value);
				this.UpdateHighlights();
			}
		}

		partial void OnWholeWordChanged(Boolean value)
		{
			if (!this._loading)
			{
				this._settings.Update(settings => settings.Editor.SearchWholeWord = value);
				this.UpdateHighlights();
			}
		}

		partial void OnUseRegexChanged(Boolean value)
		{
			this.UpdateHighlights();
		}

		partial void OnIsVisibleChanged(Boolean value)
		{
			if (!value)
			{
				this._workspace.ActiveDocument?.Editor?.ClearSearchMatches();
			}
		}

		private Boolean TryGetContext(out DocumentViewModel? document, out IEditorSurface? editor)
		{
			document = this._workspace.ActiveDocument;
			editor = document?.Editor;
			if (document is null || editor is null || this.SearchText.Length == 0)
			{
				return false;
			}

			if (!this.Query.TryValidate(out String? error))
			{
				this.StatusText = error ?? Loc.Get("Str.Shell.Search.Invalid");
				return false;
			}

			return true;
		}

		private void Select(IEditorSurface editor, TextSpan? span, String message)
		{
			if (span is { } found)
			{
				editor.Select(found.Offset, found.Length);
				this.StatusText = message;
			}
			else
			{
				this._sounds.PlayNotFound();
				this.StatusText = Loc.Get("Str.Shell.Search.NotFound");
			}
		}

		private void UpdateHighlights()
		{
			IEditorSurface? editor = this._workspace.ActiveDocument?.Editor;
			if (editor is null)
			{
				return;
			}

			if (!this.IsVisible || this.SearchText.Length == 0 || !this.Query.TryValidate(out String? _))
			{
				editor.ClearSearchMatches();
				return;
			}

			String text = this._workspace.ActiveDocument!.TextDocument.Text;
			editor.SetSearchMatches(this._searcher.FindAll(text, this.Query).Take(MaxHighlightedMatches).ToList());
		}

		private void RememberQuery()
		{
			String query = this.SearchText;
			if (query.Length == 0)
			{
				return;
			}

			this._settings.SearchHistory.AddQuery(query);
			this.History.Remove(query);
			this.History.Insert(0, query);
		}
	}
}
