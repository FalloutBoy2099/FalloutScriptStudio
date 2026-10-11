namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;
	using Wpf.Ui.Controls;

	/// <summary>
	/// View model of the settings window. It edits a copy of the settings; nothing changes until the user confirms.
	/// </summary>
	public sealed partial class SettingsViewModel : ToolWindowViewModelBase
	{
		private readonly ISettingsService _settingsService;
		private readonly IThemeService _themeService;
		private readonly IDialogService _dialogService;
		private readonly FileAssociationService _fileAssociation;
		private readonly AppTheme _initialTheme;

		[ObservableProperty]
		private SettingsPageItem? _selectedPageItem;

		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
		/// </summary>
		/// <param name="settingsService">The settings to edit.</param>
		/// <param name="themeService">Applies the color mode.</param>
		/// <param name="dialogService">Shows the folder and file pickers.</param>
		/// <param name="fileAssociation">Registers the file types.</param>
		/// <param name="fontCatalog">Lists the bundled editor fonts.</param>
		public SettingsViewModel(
			ISettingsService settingsService,
			IThemeService themeService,
			IDialogService dialogService,
			FileAssociationService fileAssociation,
			IEditorFontCatalog fontCatalog)
		{
			ArgumentNullException.ThrowIfNull(settingsService);
			ArgumentNullException.ThrowIfNull(themeService);
			ArgumentNullException.ThrowIfNull(dialogService);
			ArgumentNullException.ThrowIfNull(fileAssociation);
			ArgumentNullException.ThrowIfNull(fontCatalog);

			this._settingsService = settingsService;
			this._themeService = themeService;
			this._dialogService = dialogService;
			this._fileAssociation = fileAssociation;

			this.Draft = settingsService.Settings.Clone();
			this.Draft.Normalize();
			this._initialTheme = themeService.Current;
			this._selectedTheme = themeService.Current;

			this.FontChoices = BuildFontChoices(fontCatalog, settingsService.Paths.FontsDirectory);
			if (this.Draft.Editor.FontIndex >= this.FontChoices.Count)
			{
				this.Draft.Editor.FontIndex = 0;
			}

			this.MessagePaths = new ObservableCollection<String>(this.Draft.Paths.MessageSearchDirectories);
			this.PreprocessorDefineItems = new ObservableCollection<String>(settingsService.PreprocessorDefines.Items.Where(PreprocessorDefines.IsValidDefinition).Select(item => item.Trim()));
			this.Pages = new List<SettingsPageItem>
			{
				new SettingsPageItem(SettingsPage.Appearance, Loc.Get("Str.ToolWindows.Page.Appearance"), SymbolRegular.PaintBrush24),
				new SettingsPageItem(SettingsPage.Editor, Loc.Get("Str.ToolWindows.Page.Editor"), SymbolRegular.TextEditStyle24),
				new SettingsPageItem(SettingsPage.Parser, Loc.Get("Str.ToolWindows.Page.Parser"), SymbolRegular.BracesVariable24),
				new SettingsPageItem(SettingsPage.Compiler, Loc.Get("Str.ToolWindows.Page.Compiler"), SymbolRegular.Wrench24),
				new SettingsPageItem(SettingsPage.Paths, Loc.Get("Str.ToolWindows.Page.Paths"), SymbolRegular.FolderOpen24),
				new SettingsPageItem(SettingsPage.Messages, Loc.Get("Str.ToolWindows.Page.Messages"), SymbolRegular.Chat24),
				new SettingsPageItem(SettingsPage.Flowchart, Loc.Get("Str.ToolWindows.Page.Flowchart"), SymbolRegular.Flowchart24),
			};
			this._selectedPageItem = this.Pages[0];

			this.RefreshAssociation();
		}

		/// <summary>Gets the copy of the settings that is being edited.</summary>
		public AppSettings Draft { get; }

		/// <summary>Gets a value indicating whether the user confirmed the dialog and the settings were applied.</summary>
		public Boolean Applied { get; private set; }

		/// <summary>Gets the pages of the window.</summary>
		public IReadOnlyList<SettingsPageItem> Pages { get; }

		/// <summary>Gets the page that is currently shown.</summary>
		public SettingsPage SelectedPage => this.SelectedPageItem?.Page ?? SettingsPage.Appearance;

		/// <summary>Gets a value indicating whether the Appearance page is shown.</summary>
		public Boolean IsAppearancePage => this.SelectedPage == SettingsPage.Appearance;

		/// <summary>Gets a value indicating whether the Code editor page is shown.</summary>
		public Boolean IsEditorPage => this.SelectedPage == SettingsPage.Editor;

		/// <summary>Gets a value indicating whether the Parser page is shown.</summary>
		public Boolean IsParserPage => this.SelectedPage == SettingsPage.Parser;

		/// <summary>Gets a value indicating whether the Compiler page is shown.</summary>
		public Boolean IsCompilerPage => this.SelectedPage == SettingsPage.Compiler;

		/// <summary>Gets a value indicating whether the Paths page is shown.</summary>
		public Boolean IsPathsPage => this.SelectedPage == SettingsPage.Paths;

		/// <summary>Gets a value indicating whether the Messages page is shown.</summary>
		public Boolean IsMessagesPage => this.SelectedPage == SettingsPage.Messages;

		/// <summary>Gets a value indicating whether the Flowchart page is shown.</summary>
		public Boolean IsFlowchartPage => this.SelectedPage == SettingsPage.Flowchart;

		/// <summary>
		/// Applies the chosen color mode, validates the edited copy and hands it to the settings service.
		/// Used by the OK command.
		/// </summary>
		public void Commit()
		{
			AppSettings settings = this.Draft;

			if (this.SelectedTheme != this._initialTheme)
			{
				this._themeService.Apply(this.SelectedTheme);
				settings.Editor.Theme = this._themeService.IsDark ? HighlightTheme.Dark : HighlightTheme.Light;
			}

			Normalize(settings, this.MessagePaths, this.FontChoices.Count);
			this.CommitPreprocessorDefines();

			// Only the options of this window are copied into the live settings: values written meanwhile by other components survive.
			this._settingsService.Update(live => SettingsDialogMerge.CopyOwnedOptions(live, settings));
			this.Applied = true;
		}

		/// <summary>
		/// Repairs the values the user typed: blank paths become unset, the tab size and the font index are limited to valid values.
		/// </summary>
		/// <param name="settings">The settings to repair.</param>
		/// <param name="messagePaths">The message folders in priority order.</param>
		/// <param name="fontCount">The number of selectable fonts.</param>
		public static void Normalize(AppSettings settings, IEnumerable<String> messagePaths, Int32 fontCount)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(messagePaths);

			settings.Messages.Language = String.IsNullOrWhiteSpace(settings.Messages.Language) ? "english" : settings.Messages.Language.Trim();

			if (settings.Editor.TabSize < EditorSettings.MinTabSize || settings.Editor.TabSize > EditorSettings.MaxTabSize)
			{
				settings.Editor.TabSize = EditorSettings.DefaultTabSize;
			}

			if (settings.Editor.FontIndex < 0 || settings.Editor.FontIndex >= Math.Max(1, fontCount))
			{
				settings.Editor.FontIndex = 0;
			}

			settings.Paths.OutputDirectory = NullIfBlank(settings.Paths.OutputDirectory);
			settings.Paths.ScriptsHeaderFile = NullIfBlank(settings.Paths.ScriptsHeaderFile);
			settings.Paths.HeadersDirectory = NullIfBlank(settings.Paths.HeadersDirectory);
			settings.Paths.ExternalEditorPath = NullIfBlank(settings.Paths.ExternalEditorPath);
			settings.Compiler.ExternalDecompilerPath = NullIfBlank(settings.Compiler.ExternalDecompilerPath);
			settings.Paths.MessageSearchDirectories = messagePaths
				.Select(path => path.Trim())
				.Where(path => path.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		partial void OnSelectedPageItemChanged(SettingsPageItem? value)
		{
			this.OnPropertyChanged(nameof(this.SelectedPage));
			this.OnPropertyChanged(nameof(this.IsAppearancePage));
			this.OnPropertyChanged(nameof(this.IsEditorPage));
			this.OnPropertyChanged(nameof(this.IsParserPage));
			this.OnPropertyChanged(nameof(this.IsCompilerPage));
			this.OnPropertyChanged(nameof(this.IsPathsPage));
			this.OnPropertyChanged(nameof(this.IsMessagesPage));
			this.OnPropertyChanged(nameof(this.IsFlowchartPage));
		}

		private static String CreateFilter(String descriptionKey, String pattern)
		{
			return Loc.Get(descriptionKey) + "|" + pattern + "|" + Loc.Get("Str.ToolWindows.Filter.AllFiles") + "|*.*";
		}

		private static String? NullIfBlank(String? value)
		{
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}

		private static String? ExistingDirectory(String? path)
		{
			return !String.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? path : null;
		}

		[RelayCommand]
		private async Task OkAsync()
		{
			this.Commit();

			try
			{
				await this._settingsService.SaveAsync().ConfigureAwait(true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogService.ShowErrorAsync(Loc.Get("Str.Common.Settings"), Loc.Format("Str.ToolWindows.Settings.SaveFailed", ex.Message)).ConfigureAwait(true);
			}

			this.RequestClose(true);
		}

		[RelayCommand]
		private void Cancel()
		{
			this.RequestClose(false);
		}
	}
}
