namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace;

	/// <summary>Edit and Code menu commands, navigation and search.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Gets the procedures offered by the "Go to procedure" popup.</summary>
		public ObservableCollection<ScriptProcedure> GoToProcedureItems { get; } = new ObservableCollection<ScriptProcedure>();

		/// <summary>True while the "Go to procedure" popup is open.</summary>
		[ObservableProperty]
		public partial Boolean IsGoToProcedureOpen { get; set; }

		/// <summary>The filter text of the "Go to procedure" popup.</summary>
		[ObservableProperty]
		public partial String GoToProcedureFilter { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether trailing spaces are removed when saving.</summary>
		public Boolean AutoTrimTrailingSpaces
		{
			get => this._settings.Settings.Editor.AutoRemoveTrailingSpaces;
			set => this.SetSetting(settings => settings.Editor.AutoRemoveTrailingSpaces = value, nameof(this.AutoTrimTrailingSpaces));
		}

		/// <summary>Gets or sets a value indicating whether scripts and headers are formatted when they are opened.</summary>
		public Boolean FormatCodeOnOpen
		{
			get => this._settings.Settings.Editor.FormatCodeOnOpen;
			set => this.SetSetting(settings => settings.Editor.FormatCodeOnOpen = value, nameof(this.FormatCodeOnOpen));
		}

		/// <summary>Gets or sets a value indicating whether tabs and spaces are drawn.</summary>
		public Boolean ShowWhitespace
		{
			get => this._settings.Settings.Editor.ShowTabCharacters;
			set => this.SetSetting(settings => settings.Editor.ShowTabCharacters = value, nameof(this.ShowWhitespace));
		}

		/// <summary>Gets or sets a value indicating whether the indent guide (vertical ruler) is shown.</summary>
		public Boolean ShowIndentLine
		{
			get => this._settings.Settings.Editor.ShowVerticalRuler;
			set => this.SetSetting(settings => settings.Editor.ShowVerticalRuler = value, nameof(this.ShowIndentLine));
		}

		private IEditorSurface? ActiveEditor => this._workspace.ActiveDocument?.Editor;

		/// <summary>Undoes the last change.</summary>
		[RelayCommand]
		public void Undo()
		{
			this.ActiveEditor?.Undo();
		}

		/// <summary>Redoes the last undone change.</summary>
		[RelayCommand]
		public void Redo()
		{
			this.ActiveEditor?.Redo();
		}

		/// <summary>Cuts the selection.</summary>
		[RelayCommand]
		public void Cut()
		{
			this.ActiveEditor?.Cut();
		}

		/// <summary>Copies the selection.</summary>
		[RelayCommand]
		public void Copy()
		{
			this.ActiveEditor?.Copy();
		}

		/// <summary>Pastes the clipboard text.</summary>
		[RelayCommand]
		public void Paste()
		{
			this.ActiveEditor?.Paste();
		}

		/// <summary>Selects all text.</summary>
		[RelayCommand]
		public void SelectAll()
		{
			this.ActiveEditor?.SelectAll();
		}

		/// <summary>Runs a text command of the editor (comment, indent, case conversion, folding…).</summary>
		[RelayCommand]
		public void Edit(EditorCommand command)
		{
			IEditorSurface? editor = this.ActiveEditor;
			if (editor is not null && editor.CanExecute(command))
			{
				editor.Execute(command);
			}
		}

		/// <summary>Opens the advanced "search in files" window.</summary>
		[RelayCommand]
		public async Task AdvancedSearchAsync()
		{
			await this._scriptTools.ShowAdvancedSearchAsync(this.ActiveEditor?.WordAtCaret);
		}

		/// <summary>Shows the find / replace bar.</summary>
		[RelayCommand]
		public void ShowSearchBar()
		{
			this.Search.Show(true);
		}

		/// <summary>Takes the word at the caret into the search bar and finds its next occurrence (quick find).</summary>
		[RelayCommand]
		public void QuickFind()
		{
			if (!this.HasDocument)
			{
				return;
			}

			this.Search.Show(true);
			this.Search.FindNext();
		}

		/// <summary>Finds the next match.</summary>
		[RelayCommand]
		public void FindNext()
		{
			if (this.Search.SearchText.Length == 0)
			{
				this.Search.Show(true);
			}

			this.Search.FindNext();
		}

		/// <summary>Finds the previous match.</summary>
		[RelayCommand]
		public void FindPrevious()
		{
			if (this.Search.SearchText.Length == 0)
			{
				this.Search.Show(true);
			}

			this.Search.FindPrevious();
		}

		/// <summary>Asks for a line number and goes to it.</summary>
		[RelayCommand]
		public async Task GoToLineAsync()
		{
			IEditorSurface? editor = this.ActiveEditor;
			if (editor is null)
			{
				return;
			}

			Int32? line = await this._toolWindows.PromptGoToLineAsync(editor.CaretLine, editor.LineCount);
			if (line is { } target)
			{
				editor.GoTo(target, 1);
				editor.FocusEditor();
			}
		}

		/// <summary>Opens the "Go to procedure" popup.</summary>
		[RelayCommand]
		public void GoToProcedure()
		{
			ProgramInfo? info = this._workspace.ActiveDocument?.ProgramInfo;
			if (info is null || !this.IsParsable)
			{
				return;
			}

			this.GoToProcedureFilter = String.Empty;
			this.LoadGoToProcedureItems(info);
			this.IsGoToProcedureOpen = true;
		}

		/// <summary>Goes to the procedure chosen in the popup.</summary>
		[RelayCommand]
		public async Task GoToProcedureItemAsync(ScriptProcedure? procedure)
		{
			this.IsGoToProcedureOpen = false;
			if (procedure is null)
			{
				return;
			}

			if (procedure.StartLine > 0)
			{
				await this._workspace.GoToAsync(procedure.StartFile, procedure.StartLine);
			}
			else if (procedure.DeclaredLine > 0)
			{
				await this._workspace.GoToAsync(procedure.DeclaredFile, procedure.DeclaredLine);
			}

			this.ActiveEditor?.FocusEditor();
		}

		/// <summary>Goes back in the caret history.</summary>
		[RelayCommand]
		public void GoBack()
		{
			this.ActiveEditor?.NavigateBack();
			this.RefreshCaretState();
		}

		/// <summary>Goes forward in the caret history.</summary>
		[RelayCommand]
		public void GoForward()
		{
			this.ActiveEditor?.NavigateForward();
			this.RefreshCaretState();
		}

		/// <summary>Checks the structure of the message file in the active tab and lists the problems.</summary>
		[RelayCommand]
		public async Task CheckMessageStructureAsync()
		{
			if (this._workspace.ActiveDocument is not { Kind: DocumentKind.Message } document)
			{
				return;
			}

			MessageStructureReport report = this._messageChecker.Check(document.TextDocument.Text);
			IReadOnlyList<BuildError> errors = report.ToBuildErrors(document.FilePath ?? BuildError.NoFile);
			document.ParserErrors = errors;
			if (errors.Count == 0)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Dlg.CheckTitle"), Loc.Get("Str.Shell.Dlg.NoErrorsFound"));
				return;
			}

			this.Output.ShowTab(OutputTab.Errors);
		}

		/// <summary>Raises the zoom of the active editor by 10 percent.</summary>
		[RelayCommand]
		public void ZoomIn()
		{
			this.ChangeZoom(10);
		}

		/// <summary>Lowers the zoom of the active editor by 10 percent.</summary>
		[RelayCommand]
		public void ZoomOut()
		{
			this.ChangeZoom(-10);
		}

		/// <summary>Resets the zoom of the active editor to 100 percent.</summary>
		[RelayCommand]
		public void ResetZoom()
		{
			if (this.ActiveEditor is { } editor)
			{
				editor.ZoomPercent = 100;
				this.OnPropertyChanged(nameof(this.ZoomText));
			}
		}

		partial void OnGoToProcedureFilterChanged(String value)
		{
			if (this._workspace.ActiveDocument?.ProgramInfo is { } info)
			{
				this.LoadGoToProcedureItems(info);
			}
		}

		private void ChangeZoom(Int32 delta)
		{
			if (this.ActiveEditor is { } editor)
			{
				editor.ZoomPercent = Math.Clamp(editor.ZoomPercent + delta, 50, 400);
				this.OnPropertyChanged(nameof(this.ZoomText));
			}
		}

		private void LoadGoToProcedureItems(ProgramInfo info)
		{
			String filter = this.GoToProcedureFilter.Trim();
			this.GoToProcedureItems.Clear();
			foreach (ScriptProcedure procedure in info.Procedures
				.Where(p => p.StartLine > 0 || p.DeclaredLine > 0)
				.Where(p => filter.Length == 0 || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
				.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
			{
				this.GoToProcedureItems.Add(procedure);
			}
		}
	}
}
