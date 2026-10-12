namespace FalloutScriptStudio.Editor
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Services;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// The application services every editor view needs, gathered in one object (settings, theme, workspace, folding
	/// store, opcode descriptions…) together with the editor-wide state that is shared by all open editors
	/// (options snapshot, color palette, zoom). Register it as a singleton with
	/// <see cref="EditorServiceCollectionExtensions.AddScriptEditor"/>.
	/// </summary>
	public sealed class ScriptEditorServices
	{
		/// <summary>The smallest zoom in percent.</summary>
		public const Int32 MinZoomPercent = EditorSettings.MinZoomPercent;

		/// <summary>The largest zoom in percent.</summary>
		public const Int32 MaxZoomPercent = EditorSettings.MaxZoomPercent;

		/// <summary>The folder inside the <c>resources</c> folder where the user can put replacements of the syntax definitions.</summary>
		public const String SyntaxDirectoryName = "syntax";

		private readonly Object _opcodeGate = new Object();
		private EditorPalette? _palette;
		private OpcodeCatalog? _opcodes;
		private Boolean _opcodesShort;
		private String? _opcodesDirectory;
		private EditorFonts? _fonts;
		private Int32 _zoomPercent;

		/// <summary>Initializes a new instance of the <see cref="ScriptEditorServices"/> class.</summary>
		/// <param name="settings">The settings service.</param>
		/// <param name="theme">The theme service.</param>
		/// <param name="workspace">The workspace operations (navigation, parsing, refactoring).</param>
		/// <param name="collapsedProcedures">The store that remembers the collapsed procedures of scripts.</param>
		/// <param name="userMacros">Optional supplier of the user macro words.</param>
		/// <param name="viewOptions">The view toggles shared with the main window; a private instance is used when none is registered.</param>
		/// <param name="logger">Receives the warnings about syntax files of the user that cannot be used.</param>
		/// <param name="popups">Opens the completion list, the tooltips and the code tips; the real windows are used when none is given.</param>
		public ScriptEditorServices(
			ISettingsService settings,
			IThemeService theme,
			IWorkspace workspace,
			ICollapsedProcedureStore collapsedProcedures,
			IUserMacroSource? userMacros = null,
			EditorViewOptions? viewOptions = null,
			ILogger<HighlightingProvider>? logger = null,
			IEditorPopupPresenter? popups = null)
		{
			this.Settings = settings;
			this.Theme = theme;
			this.Workspace = workspace;
			this.CollapsedProcedures = collapsedProcedures;
			this.UserMacros = userMacros;
			this.ViewOptions = viewOptions ?? new EditorViewOptions();
			this.Popups = popups ?? new EditorPopupPresenter();
			this.Highlighting = new HighlightingProvider(Path.Combine(settings.Paths.ResourcesDirectory, SyntaxDirectoryName), logger);
			this.Options = this.CreateOptions();
			this._zoomPercent = ReadZoom(settings);
			this.ViewOptions.PropertyChanged += (_, _) => this.RefreshOptions();

			settings.SettingsChanged += (_, _) => this.OnSettingsChanged();
			theme.ThemeChanged += (_, _) => this.ThemeChanged?.Invoke(this, EventArgs.Empty);
			userMacros?.Changed += (_, _) => this.UserMacrosChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Raised when the editor options changed (settings dialog, line numbers switch).</summary>
		public event EventHandler? OptionsChanged;

		/// <summary>Raised after the application theme changed; the palette is rebuilt lazily.</summary>
		public event EventHandler? ThemeChanged;

		/// <summary>Raised when the user macro words changed.</summary>
		public event EventHandler? UserMacrosChanged;

		/// <summary>Raised when the shared zoom changed.</summary>
		public event EventHandler? ZoomChanged;

		public ISettingsService Settings { get; }

		public IThemeService Theme { get; }

		public IWorkspace Workspace { get; }

		public ICollapsedProcedureStore CollapsedProcedures { get; }

		/// <summary>Gets the optional supplier of user macro words.</summary>
		public IUserMacroSource? UserMacros { get; }

		/// <summary>Gets the presenter that opens the completion list, the tooltips and the code tips.</summary>
		public IEditorPopupPresenter Popups { get; }

		/// <summary>Gets the builder of the syntax highlighting definitions.</summary>
		public HighlightingProvider Highlighting { get; }

		/// <summary>Gets the current editor options.</summary>
		public EditorOptions Options { get; private set; }

		/// <summary>Gets the editor fonts.</summary>
		public EditorFonts Fonts => this._fonts ??= new EditorFonts(this.Settings.Paths.FontsDirectory);

		/// <summary>Gets the color palette of the current theme.</summary>
		public EditorPalette Palette
		{
			get
			{
				SyntaxTheme theme = this.Theme.SyntaxTheme;
				if (this._palette is null || !Object.ReferenceEquals(this._palette.Theme, theme))
				{
					this._palette = EditorPalette.Create(theme);
				}

				return this._palette;
			}
		}

		/// <summary>Gets the view toggles shared with the main window (line numbers, parse error underlines).</summary>
		public EditorViewOptions ViewOptions { get; }

		/// <summary>
		/// Gets or sets the zoom (font scale) shared by all editors, in percent (100 = the font size of the settings).
		/// It is stored in <see cref="EditorSettings.ZoomPercent"/>, so it survives a restart of the program.
		/// </summary>
		public Int32 ZoomPercent
		{
			get => this._zoomPercent;
			set
			{
				Int32 clamped = Math.Clamp(value, MinZoomPercent, MaxZoomPercent);

				// The setting is written without raising SettingsChanged: the zoom has its own event and a wheel turn must not rebuild the options.
				this.Settings.Settings.Editor.ZoomPercent = clamped;
				if (clamped != this._zoomPercent)
				{
					this._zoomPercent = clamped;
					this.ZoomChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>Gets the opcode and function descriptions (short or full, following the options).</summary>
		/// <returns>The catalog; empty when <c>opcodes.txt</c> does not exist.</returns>
		public OpcodeCatalog GetOpcodeCatalog()
		{
			lock (this._opcodeGate)
			{
				String directory = this.Settings.Paths.DescriptionsDirectory;
				Boolean shortDescriptions = this.Options.ShortDescriptions;
				if (this._opcodes is null || this._opcodesShort != shortDescriptions
					|| !String.Equals(this._opcodesDirectory, directory, StringComparison.OrdinalIgnoreCase))
				{
					this._opcodes = OpcodeCatalog.LoadFromFile(Path.Combine(directory, "opcodes.txt"), shortDescriptions);
					this._opcodesShort = shortDescriptions;
					this._opcodesDirectory = directory;
				}

				return this._opcodes;
			}
		}

		/// <summary>Creates the resolver that finds the files of <c>#include</c> directives with the current settings.</summary>
		/// <returns>The resolver.</returns>
		public IncludeResolver CreateIncludeResolver()
		{
			return this.Settings.Effective.ToIncludeResolver(this.Settings.Paths);
		}

		private static Int32 ReadZoom(ISettingsService settings)
		{
			return Math.Clamp(settings.Settings.Editor.ZoomPercent, MinZoomPercent, MaxZoomPercent);
		}

		private void OnSettingsChanged()
		{
			this.RefreshOptions();

			// Settings that were loaded or replaced (settings dialog) carry their own zoom.
			this.ZoomPercent = ReadZoom(this.Settings);
		}

		private EditorOptions CreateOptions()
		{
			return EditorOptions.From(this.Settings.Settings.Editor, this.ViewOptions.ShowLineNumbers, this.ViewOptions.HighlightParseErrors);
		}

		private void RefreshOptions()
		{
			this.Options = this.CreateOptions();
			this.OptionsChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
