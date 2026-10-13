namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Controllers;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;

	/// <summary>
	/// Executes the text commands of <see cref="EditorCommand"/> on an editor and tells whether they are available.
	/// The text transformations are the ones of <c>Core.TextTools</c>; this class applies their edits to the document.
	/// </summary>
	public sealed class EditorCommandHandler
	{
		private readonly EditorContext _context;
		private readonly FoldingController? _folding;
		private readonly NavigationController _navigation;
		private readonly OccurrenceController _occurrences;
		private readonly CompletionController _completion;
		private readonly Action _toggleSplit;
		private readonly BracketController? _brackets;
		private readonly FalloutScriptStudio.Core.TextTools.CodeFormatter _formatter = new FalloutScriptStudio.Core.TextTools.CodeFormatter();
		private ITextSourceVersion? _unusedVersion;
		private IReadOnlyList<UnusedVariable> _unused = Array.Empty<UnusedVariable>();

		public EditorCommandHandler(
			EditorContext context,
			FoldingController? folding,
			NavigationController navigation,
			OccurrenceController occurrences,
			CompletionController completion,
			Action toggleSplit,
			BracketController? brackets = null)
		{
			this._brackets = brackets;
			this._context = context;
			this._folding = folding;
			this._navigation = navigation;
			this._occurrences = occurrences;
			this._completion = completion;
			this._toggleSplit = toggleSplit;
		}

		/// <summary>Determines whether a command can run now.</summary>
		/// <param name="command">The command.</param>
		/// <returns><see langword="true"/> when it is available.</returns>
		public Boolean CanExecute(EditorCommand command)
		{
			Boolean script = this._context.IsScript;
			Boolean message = this._context.IsMessage;

			switch (command)
			{
				case EditorCommand.CommentSelection:
				case EditorCommand.UncommentSelection:
				case EditorCommand.ToggleLineComment:
					return !message;
				case EditorCommand.ToggleBlockComment:
					return script;
				case EditorCommand.AlignSelectionLeft:
					return script && this.SelectionSpansLines();
				case EditorCommand.DecreaseIndent:
					return script;
				case EditorCommand.IncreaseIndent:
					return !message;
				case EditorCommand.FormatDocument:
				case EditorCommand.FormatSelection:
					return script;
				case EditorCommand.CapitalizeWord:
				case EditorCommand.UpperCase:
				case EditorCommand.LowerCase:
				case EditorCommand.HighlightWord:
					return this.TryGetTarget(out _);
				case EditorCommand.ConvertHexDec:
					return this.TryGetNumberEdit() is not null;
				case EditorCommand.ConvertLeadingTabsToSpaces:
				case EditorCommand.ConvertAllTabsToSpaces:
				case EditorCommand.ConvertLeadingSpacesToTabs:
				case EditorCommand.ConvertAllSpacesToTabs:
				case EditorCommand.TrimTrailingSpaces:
				case EditorCommand.DeleteLine:
				case EditorCommand.SelectWord:
				case EditorCommand.SplitDocument:
				case EditorCommand.DeleteToLineEnd:
					return true;
				case EditorCommand.GoToMatchingBracket:
					return !message && this._brackets is not null;
				case EditorCommand.ToggleOutline:
				case EditorCommand.CollapseAll:
				case EditorCommand.ExpandAll:
				case EditorCommand.ShowOnlyCurrentProcedure:
					return this._folding is { Count: > 0 };
				case EditorCommand.GoToDeclaration:
					return this.GetSymbol() is { Declaration.Line: > 0 } || this._navigation.CanGoToProjectDeclaration();
				case EditorCommand.GoToDefinition:
					return this.GetSymbol() is { Kind: SymbolKind.Procedure, IsImported: false } || this._navigation.CanGoToProjectDefinition();
				case EditorCommand.FindReferences:
					return this.CanFindReferences();
				case EditorCommand.RenameSymbol:
					return this.GetSymbol() is { IsImported: false } && !this._context.DocumentModel.NeedsParse;
				case EditorCommand.OpenInclude:
					return this._navigation.GetIncludePath(this._context.TextArea.Caret.Line) is not null;
				case EditorCommand.ShowCompletion:
				case EditorCommand.GoToProcedureBegin:
				case EditorCommand.FindUnusedVariables:
					return script;
				case EditorCommand.ExtractProcedure:
					return script && this._context.Editor.SelectionLength > 0;
				case EditorCommand.DeleteUnusedProcedure:
					return script && this._navigation.CurrentProcedure is not null;
				case EditorCommand.RemoveUnusedVariable:
					return script && this.FindUnusedVariableAtCaret() is { CanRemove: true };
				default:
					return false;
			}
		}

		/// <summary>Runs a command as one undoable operation.</summary>
		/// <param name="command">The command.</param>
		public void Execute(EditorCommand command)
		{
			if (!this.CanExecute(command))
			{
				return;
			}

			TextDocument document = this._context.Document;
			Int32 tabSize = this._context.Options.TabSize;

			switch (command)
			{
				case EditorCommand.CommentSelection:
					this.Edit(text => CommentTools.CommentLines(text, this.GetSelectionSpan()));
					break;
				case EditorCommand.UncommentSelection:
					this.Edit(text => CommentTools.UncommentLines(text, this.GetSelectionSpan()));
					break;
				case EditorCommand.ToggleLineComment:
					this.Edit(text => DocumentEdits.ToggleLineComment(text, this.GetSelectionSpan()));
					break;
				case EditorCommand.ToggleBlockComment:
					this.Edit(text => CommentTools.ToggleBlockComment(text, this.GetSelectionSpan()));
					break;
				case EditorCommand.AlignSelectionLeft:
					this.Edit(text => WhitespaceTools.AlignToFirstLine(text, this.GetSelectionSpan()));
					break;
				case EditorCommand.DecreaseIndent:
					this.Edit(text => WhitespaceTools.DecreaseIndent(text, this.GetSelectionSpan(), tabSize));
					break;
				case EditorCommand.IncreaseIndent:
					this.Edit(text => DocumentEdits.IncreaseIndent(text, this.GetSelectionSpan(), this._context.IndentUnit));
					break;
				case EditorCommand.FormatDocument:
					this.FormatDocument();
					break;
				case EditorCommand.FormatSelection:
					this.Replace(this._formatter.FormatSelectionOrLine(document.Text, this.GetSelectionSpan()));
					break;
				case EditorCommand.CapitalizeWord:
					this.ConvertTarget(CaseTools.Capitalize);
					break;
				case EditorCommand.UpperCase:
					this.ConvertTarget(CaseTools.ToUpper);
					break;
				case EditorCommand.LowerCase:
					this.ConvertTarget(CaseTools.ToLower);
					break;
				case EditorCommand.ConvertHexDec:
					this.ConvertHexDec();
					break;
				case EditorCommand.ConvertLeadingTabsToSpaces:
					this.Edit(text => WhitespaceTools.ConvertLeadingTabsToSpaces(text, this.GetSelectionSpan(), tabSize));
					break;
				case EditorCommand.ConvertAllTabsToSpaces:
					this.Edit(text => WhitespaceTools.ConvertTabsToSpaces(text, this.GetSelectionSpan(), tabSize));
					break;
				case EditorCommand.ConvertLeadingSpacesToTabs:
					this.Edit(text => WhitespaceTools.ConvertLeadingSpacesToTabs(text, this.GetSelectionSpan(), tabSize));
					break;
				case EditorCommand.ConvertAllSpacesToTabs:
					this.Edit(text => WhitespaceTools.ConvertSpacesToTabs(text, this.GetSelectionSpan(), tabSize));
					break;
				case EditorCommand.TrimTrailingSpaces:
					this.Edit(text => WhitespaceTools.TrimTrailingWhitespace(text, this.GetSelectionSpan()));
					break;
				case EditorCommand.DeleteLine:
					AvalonEditCommands.DeleteLine.Execute(null, this._context.TextArea);
					break;
				case EditorCommand.SelectWord:
					this.SelectWord();
					break;
				case EditorCommand.DeleteToLineEnd:
					this.DeleteToLineEnd();
					break;
				case EditorCommand.GoToMatchingBracket:
					this._brackets?.GoToMatchingBracket();
					break;
				case EditorCommand.ToggleOutline:
					this._folding?.ToggleOutline();
					break;
				case EditorCommand.CollapseAll:
					this._folding?.CollapseAll();
					break;
				case EditorCommand.ExpandAll:
					this._folding?.ExpandAll();
					break;
				case EditorCommand.ShowOnlyCurrentProcedure:
					this._folding?.ShowOnlyCurrentProcedure();
					break;
				case EditorCommand.HighlightWord:
					this._occurrences.ToggleHighlight(-1, this._context);
					break;
				case EditorCommand.GoToDeclaration:
					this._navigation.GoToDeclaration();
					break;
				case EditorCommand.GoToDefinition:
					this._navigation.GoToDefinition();
					break;
				case EditorCommand.FindReferences:
					this._navigation.FindReferences();
					break;
				case EditorCommand.RenameSymbol:
					this._navigation.Rename();
					break;
				case EditorCommand.OpenInclude:
					this._navigation.OpenInclude();
					break;
				case EditorCommand.ShowCompletion:
					this._completion.Show(true);
					break;
				case EditorCommand.GoToProcedureBegin:
					this._navigation.GoTo(this._folding?.GetDeclarationRegionLine() ?? 1);
					break;
				case EditorCommand.SplitDocument:
					this._toggleSplit();
					break;
				case EditorCommand.ExtractProcedure:
					this.RunOnDocument(workspace => workspace.ExtractProcedureAsync(this._context.DocumentModel));
					break;
				case EditorCommand.DeleteUnusedProcedure:
					this.DeleteUnusedProcedure();
					break;
				case EditorCommand.FindUnusedVariables:
					this.RunOnDocument(workspace => workspace.FindUnusedVariablesAsync(this._context.DocumentModel));
					break;
				case EditorCommand.RemoveUnusedVariable:
					this.RemoveUnusedVariable();
					break;
				default:
					break;
			}
		}

		/// <summary>The selection (an empty span at the caret when nothing is selected).</summary>
		private TextSpan GetSelectionSpan()
		{
			return new TextSpan(this._context.Editor.SelectionStart, this._context.Editor.SelectionLength);
		}

		private void Edit(Func<String, IReadOnlyList<TextEdit>> compute)
		{
			DocumentEdits.Apply(this._context.Document, compute(this._context.Document.Text));
		}

		private void Replace(TextEdit? edit)
		{
			if (edit is not null)
			{
				DocumentEdits.ApplyLineWise(this._context.Document, edit);
			}
		}

		private void FormatDocument()
		{
			String text = this._context.Document.Text;
			this.Replace(TextEditApplier.CreateDiff(text, this._formatter.Format(text)));
		}

		private Boolean SelectionSpansLines()
		{
			TextEditor editor = this._context.Editor;
			if (editor.SelectionLength == 0)
			{
				return false;
			}

			TextDocument document = this._context.Document;
			return document.GetLineByOffset(editor.SelectionStart).LineNumber != document.GetLineByOffset(editor.SelectionStart + editor.SelectionLength).LineNumber;
		}

		/// <summary>Procedures need the data of the native parser to know their references; variables always have them.</summary>
		private Boolean CanFindReferences()
		{
			if (this._navigation.CanFindProjectReferences())
			{
				return true;
			}

			return this.GetSymbol() switch
			{
				{ Kind: SymbolKind.Procedure } => this._context.ProgramInfo?.HasNativeData == true,
				{ Kind: SymbolKind.Macro } or null => false,
				_ => true,
			};
		}

		private void RunOnDocument(Func<IWorkspace, System.Threading.Tasks.Task> operation)
		{
			this._context.RunInBackground(() => operation(this._context.Services.Workspace));
		}

		/// <summary>Finds the unused variable whose name is under the caret; the analysis is kept until the text changes.</summary>
		private UnusedVariable? FindUnusedVariableAtCaret()
		{
			TextDocument document = this._context.Document;
			ITextSourceVersion version = document.Version;
			if (this._unusedVersion is null || !version.BelongsToSameDocumentAs(this._unusedVersion) || version.CompareAge(this._unusedVersion) != 0)
			{
				this._unused = UnusedVariableScan.Find(document.Text, this._context.ProgramInfo, true, this._context.Kind == DocumentKind.Script, true);
				this._unusedVersion = version;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			return this._unused.FirstOrDefault(variable => caret >= variable.Offset && caret <= variable.Offset + variable.Length);
		}

		private void DeleteUnusedProcedure()
		{
			if (this._navigation.CurrentProcedure is { } procedure)
			{
				String name = procedure.Name;
				this.RunOnDocument(workspace => workspace.DeleteUnusedProcedureAsync(this._context.DocumentModel, name));
			}
		}

		private void RemoveUnusedVariable()
		{
			UnusedVariable? variable = this.FindUnusedVariableAtCaret();
			TextEdit? edit = variable is null ? null : VariableDeclarationEditor.CreateRemoval(this._context.Document.Text, variable);
			if (edit is not null)
			{
				DocumentEdits.Apply(this._context.Document, new[] { edit });
			}
		}

		private IScriptSymbol? GetSymbol()
		{
			return this._context.IsScript ? this._navigation.FindSymbol(this._context.TextArea.Caret.Offset, out _) : null;
		}

		/// <summary>Finds the text a case or highlight command works on: the selection or the word at the caret.</summary>
		private Boolean TryGetTarget(out TextSpan span)
		{
			TextEditor editor = this._context.Editor;
			if (editor.SelectionLength > 0)
			{
				span = new TextSpan(editor.SelectionStart, editor.SelectionLength);
				return true;
			}

			Boolean found = WordFinder.TryGetWordRangeNearCaret(this._context.Document, this._context.TextArea.Caret.Offset, out Int32 start, out Int32 length);
			span = new TextSpan(start, length);
			return found;
		}

		private TextEdit? TryGetNumberEdit()
		{
			return this.TryGetTarget(out TextSpan span) ? NumberBaseConverter.Convert(this._context.Document.Text, span) : null;
		}

		private void ConvertTarget(Func<String, TextSpan, TextEdit?> convert)
		{
			if (this.TryGetTarget(out TextSpan span))
			{
				this.Replace(convert(this._context.Document.Text, span));
			}
		}

		private void ConvertHexDec()
		{
			TextEdit? edit = this.TryGetNumberEdit();
			if (edit is null)
			{
				return;
			}

			DocumentEdits.Apply(this._context.Document, new[] { edit });
			this._context.TextArea.Selection = Selection.Create(this._context.TextArea, edit.Offset, edit.Offset + edit.NewText.Length);
		}

		/// <summary>Deletes from the caret to the end of the line (legacy <c>DeleteToLineEnd</c>); the line break stays.</summary>
		private void DeleteToLineEnd()
		{
			Int32 caret = this._context.TextArea.Caret.Offset;
			Int32 count = this._context.Document.GetLineByOffset(caret).EndOffset - caret;
			if (count > 0)
			{
				this._context.Document.Remove(caret, count);
			}
		}

		private void SelectWord()
		{
			if (WordFinder.TryGetWordRangeNearCaret(this._context.Document, this._context.TextArea.Caret.Offset, out Int32 start, out Int32 length))
			{
				this._context.TextArea.Selection = Selection.Create(this._context.TextArea, start, start + length);
			}
		}
	}
}
