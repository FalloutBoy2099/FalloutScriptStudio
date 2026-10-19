namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;

	/// <summary>
	/// View model of the snippet manager: the built-in snippets (read-only: customize one to replace it, or disable it), the user's snippets
	/// and the ones of the project, with add / duplicate / delete / disable, the check of the triggers and of the bodies, and the import and
	/// export of a snippet file. Nothing is written before <see cref="SaveCommand"/>.
	/// </summary>
	public sealed partial class SnippetManagerViewModel : ToolWindowViewModelBase
	{
		private readonly ISnippetService _snippets;
		private readonly IDialogService _dialogs;
		private readonly IFunctionCatalogService? _functions;
		private String _initialSignature = String.Empty;
		private Boolean _loading;
		private Boolean _closeConfirmed;
		private SnippetFileReadResult _userFile = new(SnippetFileStatus.Missing, new SnippetFile());
		private SnippetFileReadResult _projectFile = new(SnippetFileStatus.Missing, new SnippetFile());

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(DuplicateCommand), nameof(DeleteCommand), nameof(CustomizeCommand), nameof(DisableCommand))]
		private SnippetRowViewModel? _selectedRow;

		[ObservableProperty]
		private SnippetSource _target = SnippetSource.User;

		[ObservableProperty]
		private String _statusText = String.Empty;

		/// <summary>Initializes a new instance of the <see cref="SnippetManagerViewModel"/> class.</summary>
		/// <param name="snippets">The snippets and the files they are stored in.</param>
		/// <param name="dialogs">The file pickers and the messages.</param>
		/// <param name="functions">The functions tree whose code templates can be imported, or <see langword="null"/> when it is not available.</param>
		public SnippetManagerViewModel(ISnippetService snippets, IDialogService dialogs, IFunctionCatalogService? functions = null)
		{
			ArgumentNullException.ThrowIfNull(snippets);
			ArgumentNullException.ThrowIfNull(dialogs);

			this._snippets = snippets;
			this._dialogs = dialogs;
			this._functions = functions;
			this.Load();
		}

		/// <summary>Gets the snippets: the project's, the user's, then the built-in ones, each part ordered by trigger.</summary>
		public ObservableCollection<SnippetRowViewModel> Rows { get; } = [];

		/// <summary>Gets a value indicating whether a project is open, so that it can have snippets of its own.</summary>
		public Boolean HasProject => this._snippets.ProjectFilePath is not null;

		/// <summary>Gets the choices of the file a new snippet goes to.</summary>
		public IReadOnlyList<Choice<SnippetSource>> TargetChoices => this.HasProject
			? new[] { new Choice<SnippetSource>(SnippetSource.User, SnippetListItem.GetSourceText(SnippetSource.User)), new Choice<SnippetSource>(SnippetSource.Project, SnippetListItem.GetSourceText(SnippetSource.Project)) }
			: new[] { new Choice<SnippetSource>(SnippetSource.User, SnippetListItem.GetSourceText(SnippetSource.User)) };

		/// <summary>Gets a value indicating whether anything was changed since the window opened.</summary>
		public Boolean HasChanges => !String.Equals(this._initialSignature, this.GetSignature(), StringComparison.Ordinal);

		/// <summary>Gets a value indicating whether a trigger cannot be used, so that the snippets cannot be saved.</summary>
		public Boolean HasBlockingProblems => this.Rows.Any(row => row.HasTriggerIssue);

		/// <summary>Gets a value indicating whether a file cannot be read: it is left untouched, so the snippets of its source cannot be saved.</summary>
		public Boolean HasUnreadableFile => this._userFile.Status == SnippetFileStatus.Unreadable || (this.HasProject && this._projectFile.Status == SnippetFileStatus.Unreadable);

		/// <summary>Gets the line that tells why the snippets cannot be saved, or the state of the files; empty when there is nothing to say.</summary>
		public String FileMessage
		{
			get
			{
				List<String> lines = [];
				Describe(lines, this._userFile, this._snippets.UserFilePath);
				if (this.HasProject && this._snippets.ProjectFilePath is { } projectPath)
				{
					Describe(lines, this._projectFile, projectPath);
				}

				if (this.HasBlockingProblems)
				{
					lines.Add(Loc.Get("Str.ToolWindows.Snippets.SaveBlocked"));
				}

				return String.Join(Environment.NewLine, lines);
			}
		}

		/// <summary>Gets the help text about the syntax of a body.</summary>
		public String SyntaxHelp => Loc.Get("Str.ToolWindows.Snippets.SyntaxHelp");

		/// <summary>Called by a row after one of its values changed.</summary>
		internal void OnRowChanged()
		{
			if (this._loading)
			{
				return;
			}

			this.Revalidate();
		}

		/// <summary>Adds a new snippet to the file chosen in <see cref="Target"/> and selects it.</summary>
		[RelayCommand]
		public void Add()
		{
			SnippetSource source = this.GetTargetSource();
			String trigger = SnippetImporter.MakeTrigger("snippet", this.GetTriggers(source));
			SnippetRowViewModel row = this.AddRow(source, new SnippetDefinition { Trigger = trigger, Description = String.Empty, Body = "$0" });
			this.SelectedRow = row;
		}

		/// <summary>Adds a copy of the selected snippet (a built-in snippet is copied to the user's snippets) under a free trigger.</summary>
		[RelayCommand(CanExecute = nameof(HasSelection))]
		public void Duplicate()
		{
			if (this.SelectedRow is not { } row)
			{
				return;
			}

			SnippetSource source = row.IsBuiltIn ? SnippetSource.User : row.Source;
			SnippetDefinition copy = row.Definition.Clone();
			copy.Trigger = SnippetImporter.MakeTrigger(row.Trigger, this.GetTriggers(source).Concat(this.GetEffectiveTriggers()));
			copy.Description = row.Description;
			copy.Enabled = true;
			this.SelectedRow = this.AddRow(source, copy);
		}

		/// <summary>Removes the selected snippet of a file (the snippet it replaced, if any, is in effect again); a built-in snippet cannot be deleted.</summary>
		[RelayCommand(CanExecute = nameof(CanDelete))]
		public void Delete()
		{
			if (this.SelectedRow is not { IsEditable: true } row)
			{
				return;
			}

			Int32 index = this.Rows.IndexOf(row);
			_ = this.Rows.Remove(row);
			this.Revalidate();
			this.SelectedRow = this.Rows.Count == 0 ? null : this.Rows[Math.Clamp(index, 0, this.Rows.Count - 1)];
		}

		/// <summary>Makes a snippet of the user's own from the selected built-in snippet: same trigger, so it replaces the built-in one.</summary>
		[RelayCommand(CanExecute = nameof(IsBuiltInSelected))]
		public void Customize()
		{
			if (this.SelectedRow is { IsBuiltIn: true } row)
			{
				this.SelectedRow = this.Override(row, true);
			}
		}

		/// <summary>Disables the selected snippet: a built-in snippet is replaced by a disabled snippet of the user's own with the same trigger.</summary>
		[RelayCommand(CanExecute = nameof(HasSelection))]
		public void Disable()
		{
			if (this.SelectedRow is not { } row)
			{
				return;
			}

			if (row.IsBuiltIn)
			{
				this.SelectedRow = this.Override(row, false);
			}
			else
			{
				row.IsEnabled = false;
			}
		}

		/// <summary>Reads a snippet file and adds its snippets to the file chosen in <see cref="Target"/> (a snippet with the trigger of an existing one replaces it).</summary>
		/// <returns>A task that completes when the file was imported.</returns>
		[RelayCommand]
		public async Task ImportAsync()
		{
			String? path = this._dialogs.PickOpenFile(Loc.Get("Str.ToolWindows.Snippets.ImportTitle"), Loc.Get("Str.ToolWindows.Snippets.FileFilter"));
			if (path is null)
			{
				return;
			}

			Byte[] content;
			try
			{
				content = await File.ReadAllBytesAsync(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.ToolWindows.Snippets.ImportTitle"), ex.Message);
				return;
			}

			if (!SnippetFileStore.TryParse(content, out SnippetFile? file, out String? error) || file is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.ToolWindows.Snippets.ImportTitle"), Loc.Format("Str.ToolWindows.Snippets.ImportInvalid", Path.GetFileName(path), error ?? String.Empty));
				return;
			}

			SnippetSource source = this.GetTargetSource();
			Int32 imported = 0;
			Int32 skipped = 0;
			foreach (SnippetDefinition definition in file.Snippets)
			{
				SnippetTriggerIssue issue = SnippetValidator.CheckTrigger(definition.Trigger, Array.Empty<String>());
				if (issue != SnippetTriggerIssue.None)
				{
					skipped++;
					continue;
				}

				SnippetRowViewModel? same = this.Rows.FirstOrDefault(row => row.Source == source && String.Equals(row.Trigger.Trim(), definition.Trigger.Trim(), StringComparison.OrdinalIgnoreCase));
				if (same is not null)
				{
					_ = this.Rows.Remove(same);
				}

				_ = this.AddRow(source, definition.Clone());
				imported++;
			}

			this.StatusText = Loc.Format("Str.ToolWindows.Snippets.Imported", imported, skipped);
		}

		/// <summary>Writes the snippets of the file chosen in <see cref="Target"/> to a snippet file the user picks.</summary>
		/// <returns>A task that completes when the file was written.</returns>
		[RelayCommand]
		public async Task ExportAsync()
		{
			SnippetSource source = this.GetTargetSource();
			String? path = this._dialogs.PickSaveFile(Loc.Get("Str.ToolWindows.Snippets.ExportTitle"), Loc.Get("Str.ToolWindows.Snippets.FileFilter"), null, SnippetFile.UserFileName);
			if (path is null)
			{
				return;
			}

			SnippetFile file = new()
			{ Snippets = this.GetDefinitions(source) };
			try
			{
				SnippetFileStore.Write(path, file);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.ToolWindows.Snippets.ExportTitle"), ex.Message);
				return;
			}

			this.StatusText = Loc.Format("Str.ToolWindows.Snippets.Exported", file.Snippets.Count);
		}

		/// <summary>Adds the code templates of the user's functions (the functions tree) to the user's snippets, each under a trigger made from its name.</summary>
		[RelayCommand(CanExecute = nameof(CanImportFunctions))]
		public void ImportFunctions()
		{
			if (this._functions is null)
			{
				return;
			}

			List<String> used = this.GetTriggers(SnippetSource.User).Concat(this.GetEffectiveTriggers()).ToList();
			IReadOnlyList<Snippet> created = SnippetImporter.FromNodes(this._functions.Catalog.GetFunctions().Where(node => node.IsUser), used);
			SnippetRowViewModel? last = null;
			foreach (Snippet snippet in created)
			{
				last = this.AddRow(SnippetSource.User, SnippetDefinition.From(snippet));
			}

			if (last is not null)
			{
				this.SelectedRow = last;
			}

			this.StatusText = Loc.Format("Str.ToolWindows.Snippets.ImportedFunctions", created.Count);
		}

		/// <summary>Writes the snippets of the user's file and of the project's file, then closes the window.</summary>
		/// <returns>A task that completes when the snippets were saved or the save failed.</returns>
		[RelayCommand(CanExecute = nameof(CanSave))]
		public async Task SaveAsync()
		{
			String title = Loc.Get("Str.ToolWindows.Snippets.SaveFailedTitle");
			if (!this._snippets.TrySave(SnippetSource.User, this.GetDefinitions(SnippetSource.User), out String? error))
			{
				await this._dialogs.ShowErrorAsync(title, Loc.Format("Str.ToolWindows.Snippets.SaveFailed", this._snippets.UserFilePath, error ?? String.Empty));
				return;
			}

			if (this.HasProject && this._snippets.ProjectFilePath is { } projectPath && !this._snippets.TrySave(SnippetSource.Project, this.GetDefinitions(SnippetSource.Project), out error))
			{
				await this._dialogs.ShowErrorAsync(title, Loc.Format("Str.ToolWindows.Snippets.SaveFailed", projectPath, error ?? String.Empty));
				return;
			}

			this._initialSignature = this.GetSignature();
			this.RequestClose(true);
		}

		/// <summary>Closes the window; the changes are dropped after the user confirmed it.</summary>
		/// <returns>A task that completes when the window was closed or the user decided to stay.</returns>
		[RelayCommand]
		public async Task CancelAsync()
		{
			if (!await this.ConfirmCloseAsync())
			{
				return;
			}

			this.RequestClose(false);
		}

		/// <summary>Gets a value indicating whether closing the window must first ask what to do with the changes (there are changes and the user did not decide yet).</summary>
		public Boolean NeedsCloseConfirmation => !this._closeConfirmed && this.HasChanges;

		/// <summary>
		/// Asks whether the changes may be dropped when there are some. The Cancel button, the Esc key and the close button of the window all
		/// go through here, so that the three ask the same question.
		/// </summary>
		/// <returns><see langword="true"/> when the window may close: nothing was changed or the user agreed to drop the changes.</returns>
		public async Task<Boolean> ConfirmCloseAsync()
		{
			if (!this.NeedsCloseConfirmation)
			{
				return true;
			}

			if (!await this._dialogs.ConfirmAsync(Loc.Get("Str.ToolWindows.Snippets.Title"), Loc.Get("Str.ToolWindows.Snippets.Discard"), Loc.Get("Str.Common.Yes"), Loc.Get("Str.Common.No")))
			{
				return false;
			}

			this._closeConfirmed = true;
			return true;
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			foreach (SnippetRowViewModel row in this.Rows)
			{
				row.RefreshTexts();
			}

			this.OnPropertyChanged(nameof(this.TargetChoices));
			this.OnPropertyChanged(nameof(this.FileMessage));
			this.OnPropertyChanged(nameof(this.SyntaxHelp));
		}

		partial void OnSelectedRowChanged(SnippetRowViewModel? value)
		{
			this.OnPropertyChanged(nameof(this.IsEditorEnabled));
		}

		/// <summary>Gets a value indicating whether the fields of the selected snippet can be edited.</summary>
		public Boolean IsEditorEnabled => this.SelectedRow is { IsEditable: true };

		private static void Describe(List<String> lines, SnippetFileReadResult result, String path)
		{
			if (result.Status == SnippetFileStatus.Unreadable)
			{
				lines.Add(Loc.Format("Str.ToolWindows.Snippets.FileUnreadable", Path.GetFileName(path), result.Error ?? String.Empty));
			}
			else if (result.Status == SnippetFileStatus.Damaged)
			{
				lines.Add(Loc.Format("Str.ToolWindows.Snippets.FileDamaged", Path.GetFileName(path)));
			}
		}

		private Boolean HasSelection()
		{
			return this.SelectedRow is not null;
		}

		private Boolean CanDelete()
		{
			return this.SelectedRow is { IsEditable: true };
		}

		private Boolean IsBuiltInSelected()
		{
			return this.SelectedRow is { IsBuiltIn: true };
		}

		private Boolean CanImportFunctions()
		{
			return this._functions is not null;
		}

		private Boolean CanSave()
		{
			return !this.HasBlockingProblems && !this.HasUnreadableFile;
		}

		private SnippetSource GetTargetSource()
		{
			return this.Target == SnippetSource.Project && this.HasProject ? SnippetSource.Project : SnippetSource.User;
		}

		private void Load()
		{
			this._loading = true;
			try
			{
				this._userFile = this._snippets.GetFile(SnippetSource.User);
				this._projectFile = this.HasProject ? this._snippets.GetFile(SnippetSource.Project) : new SnippetFileReadResult(SnippetFileStatus.Missing, new SnippetFile());
				this.Rows.Clear();
				if (this.HasProject)
				{
					foreach (SnippetDefinition definition in this._projectFile.File.Snippets)
					{
						this.Rows.Add(new SnippetRowViewModel(this, SnippetSource.Project, definition.Clone()));
					}
				}

				foreach (SnippetDefinition definition in this._userFile.File.Snippets)
				{
					this.Rows.Add(new SnippetRowViewModel(this, SnippetSource.User, definition.Clone()));
				}

				foreach (Snippet snippet in BuiltInSnippets.All)
				{
					this.Rows.Add(new SnippetRowViewModel(this, snippet));
				}
			}
			finally
			{
				this._loading = false;
			}

			this.Revalidate();
			this._initialSignature = this.GetSignature();
			this.SelectedRow = this.Rows.FirstOrDefault();
		}

		private SnippetRowViewModel AddRow(SnippetSource source, SnippetDefinition definition)
		{
			SnippetRowViewModel row = new(this, source, definition);
			Int32 index = 0;
			while (index < this.Rows.Count && this.Rows[index].Source > source)
			{
				index++;
			}

			// Inside its source the snippet goes in the order of the triggers.
			while (index < this.Rows.Count && this.Rows[index].Source == source && String.Compare(this.Rows[index].Trigger, definition.Trigger, StringComparison.OrdinalIgnoreCase) <= 0)
			{
				index++;
			}

			this.Rows.Insert(index, row);
			this.Revalidate();
			return row;
		}

		private SnippetRowViewModel Override(SnippetRowViewModel builtIn, Boolean enabled)
		{
			SnippetRowViewModel? existing = this.Rows.FirstOrDefault(row => row.Source == SnippetSource.User && String.Equals(row.Trigger.Trim(), builtIn.Trigger, StringComparison.OrdinalIgnoreCase));
			if (existing is not null)
			{
				existing.IsEnabled = enabled;
				return existing;
			}

			SnippetDefinition copy = builtIn.Definition.Clone();
			copy.Description = builtIn.Description;
			copy.Enabled = enabled;
			return this.AddRow(SnippetSource.User, copy);
		}

		private IEnumerable<String> GetTriggers(SnippetSource source)
		{
			return this.Rows.Where(row => row.Source == source).Select(row => row.Trigger);
		}

		/// <summary>Gets the triggers in effect (the built-in snippets included), so that a made-up trigger never hides a snippet.</summary>
		private IEnumerable<String> GetEffectiveTriggers()
		{
			return this.Rows.Select(row => row.Trigger);
		}

		private List<SnippetDefinition> GetDefinitions(SnippetSource source)
		{
			return this.Rows.Where(row => row.Source == source).Select(row =>
			{
				SnippetDefinition definition = row.Definition.Clone();
				definition.Trigger = definition.Trigger.Trim();
				return definition;
			}).ToList();
		}

		private void Revalidate()
		{
			foreach (SnippetRowViewModel row in this.Rows)
			{
				// A trigger can be used once per file; a later file may use it to replace the snippet of an earlier one.
				IEnumerable<String> others = this.Rows.Where(other => other.Source == row.Source && !Object.ReferenceEquals(other, row)).Select(other => other.Trigger);
				Boolean replaced = row.Trigger.Trim().Length > 0 && this.Rows.Any(other => other.Source > row.Source && other.IsEnabled && String.Equals(other.Trigger.Trim(), row.Trigger.Trim(), StringComparison.OrdinalIgnoreCase));
				row.Update(others, replaced);
			}

			this.OnPropertyChanged(nameof(this.HasBlockingProblems));
			this.OnPropertyChanged(nameof(this.FileMessage));
			this.OnPropertyChanged(nameof(this.HasChanges));
			this.SaveCommand.NotifyCanExecuteChanged();
		}

		private String GetSignature()
		{
			StringBuilder builder = new();
			foreach (SnippetRowViewModel row in this.Rows.Where(row => row.IsEditable))
			{
				_ = builder.Append((Int32)row.Source).Append('\u0001').Append(row.Definition.Trigger).Append('\u0001').Append(row.Definition.Description).Append('\u0001')
					.Append(row.Definition.Body).Append('\u0001').Append(row.Definition.Enabled ? '1' : '0').Append('\u0002');
			}

			return builder.ToString();
		}
	}
}