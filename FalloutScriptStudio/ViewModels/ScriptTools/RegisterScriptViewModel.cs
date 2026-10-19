namespace FalloutScriptStudio.ViewModels.ScriptTools
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
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using FalloutScriptStudio.Workspace;

	/// <summary>
	/// View model of the "Scripts List Editor" window (legacy <c>RegisterScript</c>): edits <c>scripts.lst</c>,
	/// the script names of <c>scrname.msg</c> and, for a newly registered script, the definition in <c>scripts.h</c>.
	/// </summary>
	public sealed partial class RegisterScriptViewModel : ScriptToolViewModelBase
	{
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly ScriptRegistryStore _store;
		private readonly IWorkspace _workspace;
		private readonly ArchiveScriptExtractor? _archives;

		private ScriptRegistryPaths? _paths;
		private ScriptRegistry? _registry;
		private ScriptEntryViewModel? _registeredEntry;
		private Boolean _defineDone;
		private Boolean _loading;

		/// <summary>
		/// Initializes a new instance of the <see cref="RegisterScriptViewModel"/> class.
		/// </summary>
		/// <param name="settings">The settings.</param>
		/// <param name="dialogs">Shows messages.</param>
		/// <param name="store">Reads and writes the registration files.</param>
		/// <param name="workspace">Opens scripts in the editor.</param>
		/// <param name="archives">Extracts scripts from the game archive (shared with the rest of the application, so the last archive is remembered); optional.</param>
		public RegisterScriptViewModel(ISettingsService settings, IDialogService dialogs, ScriptRegistryStore store, IWorkspace workspace, ArchiveScriptExtractor? archives = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(store);
			ArgumentNullException.ThrowIfNull(workspace);

			this._settings = settings;
			this._dialogs = dialogs;
			this._store = store;
			this._workspace = workspace;
			this._archives = archives;
		}

		/// <summary>Gets the rows of the list; the position is the script id.</summary>
		public ObservableCollection<ScriptEntryViewModel> Entries { get; } = [];

		/// <summary>Gets or sets the selected row.</summary>
		[ObservableProperty]
		public partial ScriptEntryViewModel? SelectedEntry { get; set; }

		/// <summary>Gets a value indicating whether there are changes that were not written to the files.</summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
		[NotifyPropertyChangedFor(nameof(StatusText))]
		public partial Boolean IsModified { get; private set; }

		/// <summary>Gets or sets the text of the find box.</summary>
		[ObservableProperty]
		public partial String FindText { get; set; } = String.Empty;

		/// <summary>Gets or sets the column of the cell that is current in the grid (where the next find starts).</summary>
		public Int32 FindColumn { get; set; }

		/// <summary>Gets a message about the last operation; empty when there is none.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasNotice))]
		public partial String NoticeMessage { get; private set; } = String.Empty;

		/// <summary>Gets a value indicating whether a script is being registered, so a definition can be added to <c>scripts.h</c>.</summary>
		[ObservableProperty]
		public partial Boolean CanDefine { get; private set; }

		/// <summary>Gets or sets a value indicating whether the definition is added to <c>scripts.h</c> when the list is saved.</summary>
		[ObservableProperty]
		public partial Boolean AllowDefine { get; set; }

		/// <summary>Gets or sets the name of the constant defined in <c>scripts.h</c>.</summary>
		[ObservableProperty]
		public partial String DefineName { get; set; } = String.Empty;

		/// <summary>Gets a value indicating whether <see cref="NoticeMessage"/> is shown.</summary>
		public Boolean HasNotice => this.NoticeMessage.Length > 0;

		/// <summary>Gets the state of the files for the status bar.</summary>
		public String StatusText => Loc.Get(this.IsModified ? "Str.ScriptTools.RegStatusModified" : "Str.ScriptTools.RegStatusSaved");

		/// <summary>Raises the change of the texts that are computed, after the language changed.</summary>
		public void RefreshTexts()
		{
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		/// <summary>
		/// Loads the files and, for a script, adds it to the list when it is not registered yet.
		/// </summary>
		/// <param name="scriptFileName">The compiled script (<c>name.int</c>) to register, or <see langword="null"/> to only edit the list.</param>
		/// <returns><see langword="false"/> when the files cannot be edited (the reason was shown to the user).</returns>
		public async Task<Boolean> InitializeAsync(String? scriptFileName)
		{
			EffectiveSettings effective = this._settings.Effective;
			AppSettings settings = effective.Settings;
			ScriptRegistryPathResult resolved = ScriptRegistryPathResolver.Resolve(settings.Paths.OutputDirectory, settings.Paths.ScriptsHeaderFile, settings.Messages.Language, effective.ScriptsListFile);
			ScriptRegistryProblem? error = resolved.Problems.FirstOrDefault(problem => problem.IsError);
			if (error is not null || resolved.Paths is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Error"), error?.Message ?? Loc.Get("Str.ScriptTools.RegErrCannotOpen"));
				return false;
			}

			List<String> warnings = resolved.Problems
				.Where(problem => !problem.IsError && (scriptFileName is not null || problem.Kind == ScriptRegistryProblemKind.NameMessageMissing))
				.Select(problem => problem.Message)
				.ToList();
			if (warnings.Count > 0 && settings.Compiler.ShowWarnings)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.ScriptTools.RegAttention"), String.Join(Environment.NewLine, warnings));
			}

			ScriptRegistryPaths paths = resolved.Paths;
			Encoding messageEncoding = settings.Editor.FileEncoding.ToEncoding();
			ScriptRegistry registry;
			try
			{
				registry = await Task.Run(() => this._store.Load(paths, messageEncoding));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Error"), Loc.Format("Str.ScriptTools.RegErrRead", ex.Message));
				return false;
			}

			this._paths = paths;
			this._registry = registry;
			this._loading = true;
			this.Entries.Clear();
			for (Int32 i = 0; i < registry.Entries.Count; i++)
			{
				this.Entries.Add(this.CreateEntry(registry.Entries[i], i + 1));
			}

			this._loading = false;
			this.IsModified = false;
			if (scriptFileName is not null)
			{
				this.RegisterScript(scriptFileName);
			}

			return true;
		}

		/// <summary>
		/// Selects the row of a script, adding the row when the script is not registered yet.
		/// </summary>
		/// <param name="scriptFileName">The compiled script (<c>name.int</c>).</param>
		public void RegisterScript(String scriptFileName)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptFileName);

			ScriptEntryViewModel? entry = this.Entries.FirstOrDefault(item => String.Equals(item.Script, scriptFileName, StringComparison.OrdinalIgnoreCase));
			Boolean isNew = entry is null;
			if (entry is null)
			{
				entry = this.AddEntry(scriptFileName);
			}

			this._registeredEntry = entry;
			this._defineDone = false;
			this.DefineName = ScriptRegistryStore.DefinePrefix + Path.GetFileNameWithoutExtension(scriptFileName).ToUpperInvariant();
			this.CanDefine = true;
			this.SetAllowDefine(isNew && this._settings.Settings.Messages.RegisterScriptAllowDefine);
			this.SelectedEntry = entry;
			this.NoticeMessage = String.Empty;
		}

		/// <summary>
		/// Asks what to do with unsaved changes before the window closes.
		/// </summary>
		/// <returns><see langword="false"/> when the user cancelled closing.</returns>
		public async Task<Boolean> ConfirmCloseAsync()
		{
			if (!this.IsModified)
			{
				return true;
			}

			YesNoCancel answer = await this._dialogs.AskYesNoCancelAsync(Loc.Get("Str.ScriptTools.RegAttention"), Loc.Get("Str.ScriptTools.RegAskSave"));
			return answer switch
			{
				YesNoCancel.Yes => await this.SaveCoreAsync(),
				YesNoCancel.No => true,
				_ => false,
			};
		}

		partial void OnAllowDefineChanged(Boolean value)
		{
			if (this._loading || !this.CanDefine)
			{
				return;
			}

			this._settings.Update(settings => settings.Messages.RegisterScriptAllowDefine = value);
		}

		private void SetAllowDefine(Boolean value)
		{
			Boolean wasLoading = this._loading;
			this._loading = true;
			this.AllowDefine = value;
			this._loading = wasLoading;
		}

		private ScriptEntryViewModel CreateEntry(ScriptListEntry model, Int32 number)
		{
			return new ScriptEntryViewModel(model, number, this.OnEntryChanged, this.OnEntryRejected);
		}

		private ScriptEntryViewModel AddEntry(String script)
		{
			ScriptEntryViewModel entry = this.CreateEntry(new ScriptListEntry { Script = script }, this.Entries.Count + 1);
			this.Entries.Add(entry);
			this.IsModified = true;
			return entry;
		}

		private void OnEntryChanged(ScriptEntryViewModel entry)
		{
			if (this._loading)
			{
				return;
			}

			this.IsModified = true;
			this.NoticeMessage = String.Empty;
		}

		private void OnEntryRejected(String message)
		{
			this.NoticeMessage = message;
		}

		[RelayCommand]
		private void AddRow()
		{
			ScriptEntryViewModel entry = this.AddEntry("none.int");
			this.SelectedEntry = entry;
			this.NoticeMessage = String.Empty;
		}

		[RelayCommand]
		private void RemoveLastRow()
		{
			if (this.Entries.Count == 0)
			{
				return;
			}

			ScriptEntryViewModel last = this.Entries[this.Entries.Count - 1];
			this.Entries.RemoveAt(this.Entries.Count - 1);
			if (ReferenceEquals(last, this._registeredEntry))
			{
				this._registeredEntry = null;
				this.CanDefine = false;
				this.SetAllowDefine(false);
			}

			this.SelectedEntry = this.Entries.Count > 0 ? this.Entries[this.Entries.Count - 1] : null;
			this.IsModified = true;
			this.NoticeMessage = String.Empty;
		}

		[RelayCommand]
		private void FindNext()
		{
			this.Find(true);
		}

		[RelayCommand]
		private void FindPrevious()
		{
			this.Find(false);
		}

		private void Find(Boolean forward)
		{
			if (this.FindText.Trim().Length == 0)
			{
				return;
			}

			Int32 row = this.SelectedEntry is null ? -1 : this.Entries.IndexOf(this.SelectedEntry);
			(Int32 Row, Int32 Column)? found = ScriptListFinder.Find(this.Entries.Select(entry => entry.Model).ToList(), this.FindText, row, this.FindColumn, forward);
			if (found is null)
			{
				this.NoticeMessage = Loc.Get("Str.ScriptTools.RegNothingFound");
				return;
			}

			this.NoticeMessage = String.Empty;
			this.FindColumn = found.Value.Column;
			this.SelectedEntry = this.Entries[found.Value.Row];
		}

		[RelayCommand(CanExecute = nameof(IsModified))]
		private async Task SaveAsync()
		{
			await this.SaveCoreAsync();
		}

		private async Task<Boolean> SaveCoreAsync()
		{
			if (this._paths is null || this._registry is null)
			{
				return false;
			}

			ScriptRegistry registry = new(this._registry.ListEncoding);
			registry.Entries.AddRange(this.Entries.Select(entry => entry.Model));
			ScriptRegistryPaths paths = this._paths;
			Encoding messageEncoding = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			try
			{
				await Task.Run(() => this._store.Save(registry, paths, messageEncoding));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Error"), Loc.Format("Str.ScriptTools.RegErrSave", ex.Message));
				return false;
			}

			this.IsModified = false;
			this.NoticeMessage = String.Empty;
			await this.AddDefineAsync(paths);
			return true;
		}

		private async Task AddDefineAsync(ScriptRegistryPaths paths)
		{
			if (!this.CanDefine || !this.AllowDefine || this._defineDone || this._registeredEntry is null)
			{
				return;
			}

			if (paths.HeaderPath is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Error"), Loc.Get("Str.ScriptTools.RegErrDefineNoHeader"));
				return;
			}

			String name = this.DefineName.Trim();
			if (!ProcedureNameValidator.IsIdentifier(name))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Error"), Loc.Format("Str.ScriptTools.RegErrDefineBadName", name));
				return;
			}

			Int32 number = this.Entries.IndexOf(this._registeredEntry) + 1;
			ScriptListEntry model = this._registeredEntry.Model;
			String headerPath = paths.HeaderPath;
			try
			{
				DefineInsertResult result = await Task.Run(() => this._store.AddDefine(headerPath, name, number, model));
				this._defineDone = true;
				this.NoticeMessage = Loc.Format(
					result == DefineInsertResult.Added ? "Str.ScriptTools.RegDefineAdded" : "Str.ScriptTools.RegDefineExists",
					name.ToUpperInvariant(),
					Path.GetFileName(headerPath));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Error"), Loc.Format("Str.ScriptTools.RegErrDefineWrite", ex.Message));
			}
		}

		[RelayCommand]
		private async Task OpenScriptAsync(ScriptEntryViewModel? entry)
		{
			String? outputDirectory = this._settings.Effective.Settings.Paths.OutputDirectory;
			if (entry is null || entry.Script.Length == 0 || String.IsNullOrEmpty(outputDirectory))
			{
				return;
			}

			String path = Path.Combine(outputDirectory, entry.Script);
			if (!File.Exists(path))
			{
				String? extracted = await this.ExtractScriptAsync(entry.Script);
				if (extracted is null)
				{
					return;
				}

				path = extracted;
			}

			await this._workspace.OpenAsync(path);
		}

		private async Task<String?> ExtractScriptAsync(String scriptFileName)
		{
			if (this._archives is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.ScriptTools.RegErrOpenTitle"), Loc.Get("Str.ScriptTools.RegErrNoExtraction"));
				return null;
			}

			return await this._archives.ExtractAsync(scriptFileName);
		}
	}
}