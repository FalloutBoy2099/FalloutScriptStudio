namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;

	/// <summary>The view state of the window (panels, theme, language) and the first-run flow.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Stores the view state of the window.</summary>
		public void SaveLayout()
		{
			if (this._loadingLayout)
			{
				return;
			}

			this._layout.ShowRightBrowser = this.ShowRightBrowser;
			this._layout.ShowFunctionTree = this.ShowFunctionTree;
			this._layout.RightTabIndex = this.RightTabIndex;
			this._layout.LogMinimized = this.IsOutputMinimized;
			this._layout.ShowLineNumbers = this._viewOptions.ShowLineNumbers;
			this._layout.HighlightParseErrors = this._viewOptions.HighlightParseErrors;
			this._layout.Theme = this._theme.Current;
			this._layoutStore.Save(this._layout);
		}

		/// <summary>Gets the saved width of the left function tree.</summary>
		public Double FunctionTreeWidth
		{
			get => this._layout.FunctionTreeWidth;
			set => this._layout.FunctionTreeWidth = value;
		}

		/// <summary>Gets the theme stored in the layout file.</summary>
		public AppTheme StoredTheme => this._layout.Theme;

		/// <summary>Gets the interface language stored in the layout file (<see langword="null"/> when none was chosen).</summary>
		public String? StoredLanguage => this._layout.Language;

		/// <summary>Changes the language of the interface and remembers it.</summary>
		/// <param name="code">The two-letter language code.</param>
		public void ChangeLanguage(String code)
		{
			Loc.SetLanguage(code);
			this._layout.Language = Loc.Language;
			this.SaveLayout();
		}

		/// <summary>Runs the first-run flow: the settings dialog. The file types are never associated unasked: see <see cref="AssociateFileTypesAsync"/> (Help menu).</summary>
		public async Task RunFirstRunAsync()
		{
			if (this._settings.Settings.IsFirstRunCompleted)
			{
				return;
			}

			await this._toolWindows.ShowSettingsAsync();

			this._settings.Update(settings => settings.IsFirstRunCompleted = true);
			await this.AfterSettingsChangedAsync();
		}

		partial void OnShowRightBrowserChanged(Boolean value)
		{
			this.SaveLayout();
		}

		partial void OnShowFunctionTreeChanged(Boolean value)
		{
			this.SaveLayout();
		}

		partial void OnRightTabIndexChanged(Int32 value)
		{
			this.SaveLayout();
		}

		partial void OnIsOutputMinimizedChanged(Boolean value)
		{
			this.SaveLayout();
		}
	}
}
