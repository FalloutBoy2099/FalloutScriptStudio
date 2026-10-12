namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Find next and going to a result.</summary>
	public sealed partial class AdvancedSearchViewModel
	{
		private static Boolean TryMatchSelection(DocumentViewModel document, IEditorSurface editor, SearchMatcher matcher, out LineMatch? match)
		{
			match = null;
			TextDocument text = document.TextDocument;
			if (editor.SelectionStart < 0 || editor.SelectionStart + editor.SelectionLength > text.TextLength)
			{
				return false;
			}

			DocumentLine line = text.GetLineByOffset(editor.SelectionStart);
			Int32 relative = editor.SelectionStart - line.Offset;
			if (relative + editor.SelectionLength > line.Length)
			{
				return false;
			}

			String lineText = text.GetText(line.Offset, line.Length);
			match = matcher.FindAll(lineText).FirstOrDefault(item => item.Index == relative && item.Length == editor.SelectionLength);
			return match is not null;
		}

		[RelayCommand(CanExecute = nameof(CanReplace))]
		private async Task FindNextAsync()
		{
			if (this.TryCreateMatcher(out SearchMatcher? matcher) && matcher is not null)
			{
				await this.FindNextCoreAsync(matcher);
			}
		}

		private async Task FindNextCoreAsync(SearchMatcher matcher)
		{
			List<DocumentViewModel> documents = this.GetDocumentsInScope();
			DocumentViewModel? active = this._workspace.ActiveDocument;
			if (documents.Count == 0 || active is null)
			{
				this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoScript");
				return;
			}

			Int32 firstIndex = Math.Max(documents.IndexOf(active), 0);
			DocumentViewModel first = documents[firstIndex];
			IEditorSurface? editor = first.Editor;
			Int32 start = editor is null ? 0 : (editor.SelectionLength > 0 ? editor.SelectionStart + editor.SelectionLength : editor.CaretOffset);

			TextMatch? found = matcher.FindNext(first.TextDocument.Text, start, false);
			DocumentViewModel? foundIn = found is null ? null : first;
			for (Int32 i = 1; found is null && i < documents.Count; i++)
			{
				DocumentViewModel document = documents[(firstIndex + i) % documents.Count];
				found = matcher.FindNext(document.TextDocument.Text, 0, false);
				foundIn = document;
			}

			if (found is null)
			{
				found = matcher.FindNext(first.TextDocument.Text, 0, false);
				foundIn = first;
			}

			if (found is null || foundIn is null)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchNotFound");
				return;
			}

			this.StatusText = String.Empty;
			await this.NavigateAsync(foundIn, found.Value.Line, found.Value.Column, found.Value.Length);
		}

		private async Task NavigateAsync(DocumentViewModel document, Int32 line, Int32 column, Int32 length)
		{
			if (document.FilePath is not null)
			{
				await this._workspace.GoToAsync(document.FilePath, line, column, length);
				return;
			}

			this._workspace.ActiveDocument = document;
			document.Editor?.GoTo(line, column, length);
		}

		[RelayCommand]
		private async Task GoToResultAsync(SearchResultItem? item)
		{
			SearchResultItem? target = item ?? this.SelectedResult;
			if (target is null)
			{
				return;
			}

			SearchHit hit = target.Hit;
			await this._workspace.GoToAsync(hit.FilePath == BuildError.NoFile ? null : hit.FilePath, hit.Line, hit.Column, hit.Length);
		}
	}
}
