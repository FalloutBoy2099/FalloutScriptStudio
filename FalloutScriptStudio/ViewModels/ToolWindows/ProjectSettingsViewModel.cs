namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.ViewModels.Main;

	/// <summary>
	/// View model of the project settings window: for every option that a project can override, the choice "inherit the global setting /
	/// override it" and the value. Nothing changes until the user saves; the result goes to <c>fss-project.json</c>, never to the global settings.
	/// </summary>
	public sealed partial class ProjectSettingsViewModel : ToolWindowViewModelBase
	{
		private readonly IProjectService _projects;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private ProjectSettings _original = new ProjectSettings();

		[ObservableProperty]
		private String _projectName = String.Empty;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsEditable))]
		[NotifyPropertyChangedFor(nameof(CanCreate))]
		[NotifyPropertyChangedFor(nameof(IsMissing))]
		[NotifyPropertyChangedFor(nameof(IsDamaged))]
		[NotifyPropertyChangedFor(nameof(IsUnreadable))]
		private ProjectFileStatus _fileStatus;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectSettingsViewModel"/> class and loads the settings of the active project.
		/// </summary>
		/// <param name="projects">The project service.</param>
		/// <param name="settings">The global settings, the source of the inherited values.</param>
		/// <param name="dialogs">The folder and file pickers and the message boxes.</param>
		public ProjectSettingsViewModel(IProjectService projects, ISettingsService settings, IDialogService dialogs)
		{
			ArgumentNullException.ThrowIfNull(projects);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._projects = projects;
			this._settings = settings;
			this._dialogs = dialogs;

			this.MessagePathsMode = new OverrideMode(false);
			this.MessagePathsMode.PropertyChanged += this.OnMessagePathsModeChanged;
			this.ProjectDefines.CollectionChanged += (_, _) => this.SynchronizeDefineChoices();
			this.Load();
		}

		/// <summary>Gets a value indicating whether the user saved the settings.</summary>
		public Boolean Applied { get; private set; }

		/// <summary>Gets the folder of the project.</summary>
		public String ProjectFolder => this._projects.Folder ?? String.Empty;

		/// <summary>Gets the path of the project file.</summary>
		public String ProjectFilePath => this._projects.Folder is { } folder ? ProjectSettingsFile.GetPath(folder) : String.Empty;

		/// <summary>Gets the name shown when the project has no name of its own: the name of its folder.</summary>
		public String DefaultName => this._projects.Folder is { } folder ? (Path.GetFileName(folder).Length > 0 ? Path.GetFileName(folder) : folder) : String.Empty;

		/// <summary>Gets a value indicating whether the options can be edited (the project has a settings file).</summary>
		public Boolean IsEditable => this.FileStatus == ProjectFileStatus.Loaded;

		/// <summary>Gets a value indicating whether a settings file can be created for the project.</summary>
		public Boolean CanCreate => this.FileStatus is ProjectFileStatus.Missing or ProjectFileStatus.Damaged;

		/// <summary>Gets a value indicating whether the project has no settings file yet.</summary>
		public Boolean IsMissing => this.FileStatus == ProjectFileStatus.Missing;

		/// <summary>Gets a value indicating whether the settings file is damaged.</summary>
		public Boolean IsDamaged => this.FileStatus == ProjectFileStatus.Damaged;

		/// <summary>Gets a value indicating whether the settings file cannot be read.</summary>
		public Boolean IsUnreadable => this.FileStatus == ProjectFileStatus.Unreadable;

		/// <summary>Gets the folder with the header files.</summary>
		public ProjectOption<String> HeadersDirectory { get; private set; } = TextOption(null, null);

		/// <summary>Gets the path of <c>scripts.h</c>.</summary>
		public ProjectOption<String> ScriptsHeaderFile { get; private set; } = TextOption(null, null);

		/// <summary>Gets the folder for compiled scripts.</summary>
		public ProjectOption<String> OutputDirectory { get; private set; } = TextOption(null, null);

		/// <summary>Gets the path of <c>scripts.lst</c> (empty: it is looked for in the output folder).</summary>
		public ProjectOption<String> ScriptsListFile { get; private set; } = TextOption(null, null);

		/// <summary>Gets the option "compile next to the source file".</summary>
		public ProjectOption<Boolean> CompileToSourceDirectory { get; private set; } = FlagOption(false, null);

		/// <summary>Gets the option "search includes".</summary>
		public ProjectOption<Boolean> SearchIncludePath { get; private set; } = FlagOption(false, null);

		/// <summary>Gets the option "show compiler warnings".</summary>
		public ProjectOption<Boolean> ShowWarnings { get; private set; } = FlagOption(false, null);

		/// <summary>Gets the option "debug information".</summary>
		public ProjectOption<Boolean> DebugInfo { get; private set; } = FlagOption(false, null);

		/// <summary>Gets the option "short-circuit evaluation".</summary>
		public ProjectOption<Boolean> ShortCircuit { get; private set; } = FlagOption(false, null);

		/// <summary>Gets the option "backward compatibility".</summary>
		public ProjectOption<Boolean> BackwardCompatibility { get; private set; } = FlagOption(false, null);

		/// <summary>Gets the preprocessor.</summary>
		public ProjectOption<PreprocessorKind> Preprocessor { get; private set; } = new ProjectOption<PreprocessorKind>(PreprocessorKind.Builtin, false, PreprocessorKind.Builtin);

		/// <summary>Gets the optimization level.</summary>
		public ProjectOption<OptimizationLevel> Optimization { get; private set; } = new ProjectOption<OptimizationLevel>(OptimizationLevel.None, false, OptimizationLevel.None);

		/// <summary>Gets the selected preprocessor definition (empty for none).</summary>
		public ProjectOption<String> SelectedDefine { get; private set; } = TextOption(null, null);

		/// <summary>Gets the choice for the message folders.</summary>
		public OverrideMode MessagePathsMode { get; }

		/// <summary>Gets the folders searched for message files while the project overrides them; folders at the top have priority.</summary>
		public ObservableCollection<String> MessagePaths { get; } = new ObservableCollection<String>();

		/// <summary>Gets the definitions of the project, offered in the toolbar in addition to the global ones.</summary>
		public ObservableCollection<String> ProjectDefines { get; } = new ObservableCollection<String>();

		/// <summary>Gets the definitions the selected definition can be chosen from (the empty one means none).</summary>
		public ObservableCollection<PreprocessorDefineOption> DefineChoices { get; } = new ObservableCollection<PreprocessorDefineOption>();

		/// <summary>
		/// Reads the settings of the project again (the window opens with them, and again after the settings file was created).
		/// </summary>
		public void Load()
		{
			AppSettings global = this._settings.Settings;
			ProjectSettings project = this._projects.Settings ?? new ProjectSettings();
			this._original = project.Clone();
			this.FileStatus = this._projects.FileStatus;
			this.ProjectName = project.Name ?? String.Empty;

			this.HeadersDirectory = TextOption(global.Paths.HeadersDirectory, project.HeadersDirectory);
			this.ScriptsHeaderFile = TextOption(global.Paths.ScriptsHeaderFile, project.ScriptsHeaderFile);
			this.OutputDirectory = TextOption(global.Paths.OutputDirectory, project.OutputDirectory);
			this.ScriptsListFile = TextOption(null, project.ScriptsListFile);
			this.CompileToSourceDirectory = FlagOption(global.Compiler.CompileToSourceDirectory, project.CompileToSourceDirectory);
			this.SearchIncludePath = FlagOption(global.Compiler.SearchIncludePath, project.SearchIncludePath);
			this.ShowWarnings = FlagOption(global.Compiler.ShowWarnings, project.ShowWarnings);
			this.DebugInfo = FlagOption(global.Compiler.DebugInfo, project.DebugInfo);
			this.ShortCircuit = FlagOption(global.Compiler.ShortCircuit, project.ShortCircuit);
			this.BackwardCompatibility = FlagOption(global.Compiler.BackwardCompatibility, project.BackwardCompatibility);
			this.Preprocessor = new ProjectOption<PreprocessorKind>(global.Compiler.Preprocessor, project.Preprocessor.HasValue, project.Preprocessor ?? global.Compiler.Preprocessor);
			this.Optimization = new ProjectOption<OptimizationLevel>(global.Compiler.Optimization, project.Optimization.HasValue, project.Optimization ?? global.Compiler.Optimization);
			this.SelectedDefine = TextOption(global.Compiler.PreprocessorDefine, project.PreprocessorDefine);
			this.SelectedDefine.PropertyChanged += (_, _) => this.SynchronizeDefineChoices();

			this.MessagePathsMode.IsOverridden = false;
			this.ResetMessagePaths(project.MessageSearchDirectories ?? global.Paths.MessageSearchDirectories);
			this.MessagePathsMode.IsOverridden = project.MessageSearchDirectories is not null;

			this.ProjectDefines.Clear();
			foreach (String define in project.Defines)
			{
				this.ProjectDefines.Add(define);
			}

			this.SynchronizeDefineChoices();
			this.OnPropertyChanged(String.Empty);
		}

		/// <summary>
		/// Builds the settings the window describes: the options that inherit are <see langword="null"/>; the properties of the file this
		/// version does not know are kept.
		/// </summary>
		/// <returns>The settings, with absolute paths.</returns>
		public ProjectSettings ToSettings()
		{
			ProjectSettings result = this._original.Clone();
			result.Name = String.IsNullOrWhiteSpace(this.ProjectName) ? null : this.ProjectName.Trim();
			result.HeadersDirectory = TextValue(this.HeadersDirectory);
			result.ScriptsHeaderFile = TextValue(this.ScriptsHeaderFile);
			result.OutputDirectory = TextValue(this.OutputDirectory);
			result.ScriptsListFile = TextValue(this.ScriptsListFile);
			result.CompileToSourceDirectory = StructValue(this.CompileToSourceDirectory);
			result.SearchIncludePath = StructValue(this.SearchIncludePath);
			result.ShowWarnings = StructValue(this.ShowWarnings);
			result.DebugInfo = StructValue(this.DebugInfo);
			result.ShortCircuit = StructValue(this.ShortCircuit);
			result.BackwardCompatibility = StructValue(this.BackwardCompatibility);
			result.Preprocessor = StructValue(this.Preprocessor);
			result.Optimization = StructValue(this.Optimization);
			result.PreprocessorDefine = TextValue(this.SelectedDefine);
			result.Defines = this.ProjectDefines.ToList();
			result.MessageSearchDirectories = this.MessagePathsMode.IsOverridden ? this.MessagePaths.ToList() : null;
			return result;
		}

		private static ProjectOption<String> TextOption(String? globalValue, String? projectValue)
		{
			return new ProjectOption<String>(globalValue ?? String.Empty, projectValue is not null, projectValue ?? String.Empty);
		}

		private static ProjectOption<Boolean> FlagOption(Boolean globalValue, Boolean? projectValue)
		{
			return new ProjectOption<Boolean>(globalValue, projectValue.HasValue, projectValue ?? globalValue);
		}

		private static String? TextValue(ProjectOption<String> option)
		{
			return option.IsOverridden ? (option.Value ?? String.Empty).Trim() : null;
		}

		private static T? StructValue<T>(ProjectOption<T> option)
			where T : struct
		{
			return option.IsOverridden ? option.Value : null;
		}

		private static String CreateFilter(String descriptionKey, String pattern)
		{
			return Loc.Get(descriptionKey) + "|" + pattern + "|" + Loc.Get("Str.ToolWindows.Filter.AllFiles") + "|*.*";
		}

		private String? ExistingDirectory(String? path)
		{
			if (!String.IsNullOrWhiteSpace(path) && Directory.Exists(path))
			{
				return path;
			}

			return this._projects.Folder is { } folder && Directory.Exists(folder) ? folder : null;
		}

		private void OnMessagePathsModeChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(OverrideMode.IsOverridden) && !this.MessagePathsMode.IsOverridden)
			{
				this.ResetMessagePaths(this._settings.Settings.Paths.MessageSearchDirectories);
			}
		}

		private void ResetMessagePaths(IEnumerable<String> paths)
		{
			this.MessagePaths.Clear();
			foreach (String path in paths)
			{
				this.MessagePaths.Add(path);
			}

			this.SelectedMessagePath = null;
		}

		[RelayCommand(CanExecute = nameof(CanCreate))]
		private void CreateSettings()
		{
			if (this._projects.CreateSettings())
			{
				this.Load();
			}
		}

		[RelayCommand(CanExecute = nameof(IsEditable))]
		private async Task SaveAsync()
		{
			if (!this._projects.SaveSettings(this.ToSettings()))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.ToolWindows.Project.Title"), Loc.Format("Str.ToolWindows.Project.SaveFailed", this.ProjectFilePath)).ConfigureAwait(true);
				return;
			}

			this.Applied = true;
			this.RequestClose(true);
		}

		[RelayCommand]
		private void Cancel()
		{
			this.RequestClose(false);
		}

		partial void OnFileStatusChanged(ProjectFileStatus value)
		{
			this.CreateSettingsCommand.NotifyCanExecuteChanged();
			this.SaveCommand.NotifyCanExecuteChanged();
		}
	}
}
