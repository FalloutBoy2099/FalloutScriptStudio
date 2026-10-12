namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Replacing in the current script, the open scripts and (for a single script) one match at a time.</summary>
	public sealed partial class AdvancedSearchViewModel
	{
		[RelayCommand(CanExecute = nameof(CanReplace))]
		private async Task ReplaceAsync()
		{
			if (!this.TryCreateMatcher(out SearchMatcher? matcher) || matcher is null)
			{
				return;
			}

			DocumentViewModel? document = this._workspace.ActiveDocument;
			IEditorSurface? editor = document?.Editor;
			if (document is not null && editor is not null && editor.SelectionLength > 0
				&& TryMatchSelection(document, editor, matcher, out LineMatch? match) && match is not null)
			{
				editor.ReplaceSelection(matcher.Expand(match, this.ReplaceText));
			}

			await this.FindNextCoreAsync(matcher);
		}

		[RelayCommand(CanExecute = nameof(CanReplace))]
		private async Task ReplaceAllAsync()
		{
			if (!this.TryCreateMatcher(out SearchMatcher? matcher) || matcher is null)
			{
				return;
			}

			List<DocumentViewModel> documents = this.GetDocumentsInScope();
			if (documents.Count == 0)
			{
				this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoScript");
				return;
			}

			List<(DocumentViewModel Document, IReadOnlyList<TextEdit> Edits)> plans = new List<(DocumentViewModel, IReadOnlyList<TextEdit>)>();
			foreach (DocumentViewModel document in documents)
			{
				IReadOnlyList<TextEdit> edits;
				try
				{
					edits = matcher.ComputeReplacements(document.TextDocument.Text, this.ReplaceText);
				}
				catch (RegexMatchTimeoutException)
				{
					this.StatusText = String.Empty;
					this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrRegexTimeout");
					return;
				}

				if (edits.Count > 0)
				{
					plans.Add((document, edits));
				}
			}

			Int32 total = plans.Sum(plan => plan.Edits.Count);
			if (total == 0)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchNotFound");
				return;
			}

			if (plans.Count > 1
				&& !await this._dialogs.ConfirmAsync(
					Loc.Get("Str.ScriptTools.SearchReplaceAll"),
					Loc.Format("Str.ScriptTools.SearchConfirmReplaceAll", total, plans.Count),
					Loc.Get("Str.Common.Replace"),
					Loc.Get("Str.Common.Cancel")))
			{
				return;
			}

			foreach ((DocumentViewModel document, IReadOnlyList<TextEdit> edits) in plans)
			{
				ApplyEdits(document.TextDocument, edits);
			}

			this.StatusText = Loc.Format("Str.ScriptTools.SearchReplaced", total, plans.Count);
			this._workspace.ReportStatus(this.StatusText);
		}

		private List<DocumentViewModel> GetDocumentsInScope()
		{
			if (this.Scope == SearchScope.CurrentScript)
			{
				return this._workspace.ActiveDocument is null ? new List<DocumentViewModel>() : new List<DocumentViewModel> { this._workspace.ActiveDocument };
			}

			return this._workspace.Documents.ToList();
		}

		private static void ApplyEdits(TextDocument document, IReadOnlyList<TextEdit> edits)
		{
			document.UndoStack.StartUndoGroup();
			try
			{
				document.BeginUpdate();
				try
				{
					for (Int32 i = edits.Count - 1; i >= 0; i--)
					{
						document.Replace(edits[i].Offset, edits[i].Length, edits[i].NewText);
					}
				}
				finally
				{
					document.EndUpdate();
				}
			}
			finally
			{
				document.UndoStack.EndUndoGroup();
			}
		}
	}
}
