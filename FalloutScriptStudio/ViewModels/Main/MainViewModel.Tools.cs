namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;

	/// <summary>Script, Headers, Dialog, Compile, Options and Help menu commands.</summary>
	public sealed partial class MainViewModel
	{
		private const Int32 MaxRegisteredNameLength = 12;

		/// <summary>Gets or sets a value indicating whether the registration dialog adds a define to scripts.h.</summary>
		public Boolean DefineToScriptsHeader
		{
			get => this._settings.Settings.Messages.RegisterScriptAllowDefine;
			set => this.SetSetting(settings => settings.Messages.RegisterScriptAllowDefine = value, nameof(this.DefineToScriptsHeader));
		}

		/// <summary>Gets or sets a value indicating whether the message editor opens automatically with a script.</summary>
		public Boolean AutoOpenMessageEditor
		{
			get => this._settings.Settings.Messages.OpenEditorAutomatically;
			set => this.SetSetting(settings => settings.Messages.OpenEditorAutomatically = value, nameof(this.AutoOpenMessageEditor));
		}

		/// <summary>Gets or sets a value indicating whether the decompiler produces Fallout 1 code.</summary>
		public Boolean DecompileAsFallout1
		{
			get => this._settings.Settings.Compiler.DecompileForFallout1;
			set => this.SetSetting(settings => settings.Compiler.DecompileForFallout1 = value, nameof(this.DecompileAsFallout1));
		}

		/// <summary>Gets a value indicating whether the old decompiler option has an effect: only the external int2ssl decompiler has an old version.</summary>
		public Boolean CanUseOldDecompiler => this._settings.Settings.Compiler.Decompiler == DecompilerKind.ExternalInt2Ssl;

		/// <summary>Gets or sets a value indicating whether only the old decompiler is used.</summary>
		public Boolean UseOldDecompiler
		{
			get => this._settings.Settings.Compiler.UseLegacyDecompiler;
			set => this.SetSetting(settings => settings.Compiler.UseLegacyDecompiler = value, nameof(this.UseOldDecompiler));
		}

		/// <summary>Gets or sets a value indicating whether message files use the DOS Cyrillic code page.</summary>
		public Boolean UseOemMessageEncoding
		{
			get => this._settings.Settings.Editor.FileEncoding == TextEncodingKind.Oem866;
			set
			{
				if (value)
				{
					this.SetMessageEncoding(TextEncodingKind.Oem866);
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether message files use the Windows code page.</summary>
		public Boolean UseWindowsMessageEncoding
		{
			get => this._settings.Settings.Editor.FileEncoding == TextEncodingKind.Ansi;
			set
			{
				if (value)
				{
					this.SetMessageEncoding(TextEncodingKind.Ansi);
				}
			}
		}

		/// <summary>Opens the scripts list editor without registering a script.</summary>
		[RelayCommand]
		public async Task ScriptsListAsync()
		{
			await this._scriptTools.ShowRegisterScriptAsync(null);
		}

		/// <summary>Checks the active script and registers it in scripts.lst.</summary>
		[RelayCommand]
		public async Task RegisterScriptAsync()
		{
			if (this._workspace.ActiveDocument is not { } document)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Dlg.ScriptsListTitle"), Loc.Get("Str.Shell.Dlg.OpenScriptFirst"));
				return;
			}

			if (document.FilePath is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ScriptsListTitle"), Loc.Get("Str.Shell.Dlg.CannotRegisterUnsaved"));
				return;
			}

			String extension = Path.GetExtension(document.FileName).ToLowerInvariant();
			if (extension is not (".ssl" or ".int"))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ScriptsListTitle"), Loc.Get("Str.Shell.Dlg.CannotRegisterFile"));
				return;
			}

			String compiledName = Path.ChangeExtension(document.FileName, "int");
			if (compiledName.Length > MaxRegisteredNameLength)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ScriptsListTitle"), Loc.Get("Str.Shell.Dlg.NameTooLong"));
				return;
			}

			if (compiledName.Contains(' ', StringComparison.Ordinal))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ScriptsListTitle"), Loc.Get("Str.Shell.Dlg.NameHasSpaces"));
				return;
			}

			// The "global script" confirmation is asked once, by ShowRegisterScriptAsync (ScriptRegistrationChecker).
			await this._scriptTools.ShowRegisterScriptAsync(document);
		}

		/// <summary>Asks for a header and inserts an <c>#include</c> line for it.</summary>
		[RelayCommand]
		public async Task IncludeFileToCodeAsync()
		{
			if (this._workspace.ActiveDocument is not { IsParsable: true } document || document.Editor is not { } editor)
			{
				return;
			}

			// Ctrl+click in the picker inserts the include and keeps the picker open for the next header (legacy behaviour).
			String? header = await this._toolWindows.PickHeaderAsync(chosen =>
			{
				this.InsertInclude(document, editor, chosen);
				return Task.CompletedTask;
			});
			if (header is null)
			{
				return;
			}

			this.InsertInclude(document, editor, header);
		}

		private void InsertInclude(DocumentViewModel document, IEditorSurface editor, String header)
		{
			String includePath = IncludePathFormatter.Format(header, this._settings.Effective.Settings.Paths.HeadersDirectory);
			IncludePasteResult result = this._includeInserter.CreateInclude(document.TextDocument.Text, includePath, editor.CaretLine);
			document.TextDocument.ApplyEdits(new[] { result.Edit });
			editor.GoTo(result.Line, 1);

			// Legacy: the pasted include line stays highlighted until the next edit.
			ICSharpCode.AvalonEdit.Document.DocumentLine inserted = document.TextDocument.GetLineByNumber(Math.Clamp(result.Line, 1, document.TextDocument.LineCount));
			editor.HighlightInsertedText(inserted.Offset, inserted.Length);
		}

		/// <summary>Opens header files chosen in a file dialog.</summary>
		[RelayCommand]
		public async Task OpenHeaderFileAsync()
		{
			IReadOnlyList<String> files = this._dialogs.PickOpenFiles(Loc.Get("Str.Shell.Dlg.SelectHeadersTitle"), Loc.Get("Str.Shell.Filter.Header"), this._settings.Effective.Settings.Paths.HeadersDirectory);
			foreach (String file in files)
			{
				await this._workspace.OpenAsync(file, new OpenDocumentOptions { AddToRecent = false });
			}
		}

		/// <summary>Opens a header from the headers folder list.</summary>
		[RelayCommand]
		public async Task BrowseHeadersAsync()
		{
			// Ctrl+click in the picker opens the header and keeps the picker open (legacy behaviour).
			String? header = await this._toolWindows.PickHeaderAsync(async chosen => await this._workspace.OpenAsync(chosen, new OpenDocumentOptions { AddToRecent = false }));
			if (header is not null)
			{
				await this._workspace.OpenAsync(header, new OpenDocumentOptions { AddToRecent = false });
			}
		}

		/// <summary>Opens all files included by the active script.</summary>
		[RelayCommand]
		public async Task OpenAllIncludesAsync()
		{
			if (this._workspace.ActiveDocument is not { FilePath: not null, IsParsable: true } document)
			{
				return;
			}

			IncludeResolver resolver = this._settings.Effective.ToIncludeResolver(this._settings.Paths);
			foreach (String include in resolver.GetAllIncludes(document.TextDocument.Text, document.FilePath).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (File.Exists(include))
				{
					await this._workspace.OpenAsync(include, new OpenDocumentOptions { AddToRecent = false, Activate = false });
				}
			}
		}

		/// <summary>Opens the message editor for the active script (or an empty one).</summary>
		[RelayCommand]
		public async Task OpenMessageEditorAsync()
		{
			if (this._workspace.ActiveDocument is { Kind: DocumentKind.Script, FilePath: not null } document)
			{
				await this._workspace.OpenMessageEditorAsync(document);
			}
			else
			{
				this._messageEditor.Open((String?)null);
			}
		}

		/// <summary>Finds the message file of the active script and remembers it.</summary>
		[RelayCommand]
		public async Task AssociateMessageFileAsync()
		{
			if (this._workspace.ActiveDocument is not { } document)
			{
				return;
			}

			if (this.AutoOpenMessageEditor && document.Kind == DocumentKind.Script)
			{
				await this._workspace.OpenMessageEditorAsync(document);
			}
			else if (await this._workspace.AssociateMessageFileAsync(document, true, true) is { } path)
			{
				// Like the legacy editor: the associated message file is opened in a text tab.
				await this._workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = false });
			}
		}

		/// <summary>Opens the dialogue flowchart of the active script.</summary>
		[RelayCommand]
		public async Task ShowFlowchartAsync()
		{
			if (await this.GetDialogueScriptAsync(Loc.Get("Str.Shell.Dlg.FlowchartTitle")) is { } document)
			{
				await this._scriptTools.ShowFlowchartAsync(document);
			}
		}

		/// <summary>Opens the dialogue preview of the active script.</summary>
		[RelayCommand]
		public async Task ShowDialoguePreviewAsync()
		{
			if (await this.GetDialogueScriptAsync(Loc.Get("Str.Shell.Dlg.PreviewTitle")) is { } document)
			{
				await this._scriptTools.ShowDialoguePreviewAsync(document);
			}
		}

		/// <summary>
		/// Compares the active script with its message file and lists the differences in the Errors tab: the message numbers the script uses
		/// and the file lacks, the unused, duplicate and empty messages. A script and a message file without differences give a short note.
		/// </summary>
		[RelayCommand]
		public async Task CheckMessagesAsync()
		{
			if (this._workspace.ActiveDocument is not { Kind: DocumentKind.Script } document)
			{
				return;
			}

			if (document.FilePath is null)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Dlg.MessageFileTitle"), Loc.Get("Str.Shell.Dlg.SaveScriptFirst"));
				return;
			}

			if (await this._workspace.CheckMessagesAsync(document) == 0)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Dlg.CheckTitle"), Loc.Get("Str.Shell.Dlg.NoErrorsFound"));
				return;
			}

			this.Output.ShowTab(OutputTab.Errors);
		}

		/// <summary>
		/// Checks the dialogue of the active script and lists the weak points in the Errors tab: nodes that nothing leads to, replies without a way out,
		/// options that jump to an unknown procedure, groups of nodes without an exit, nodes without text, doubled options and doubled message numbers.
		/// </summary>
		[RelayCommand]
		public async Task CheckDialogueAsync()
		{
			if (this._workspace.ActiveDocument is not { Kind: DocumentKind.Script } document)
			{
				return;
			}

			String title = Loc.Get("Str.Shell.Dlg.CheckDialogueTitle");
			if (document.FilePath is null)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.SaveScriptFirstDialogue"));
				return;
			}

			DialogueCheckResult result = await this._workspace.CheckDialogueAsync(document);
			if (!result.HasDialogue)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.NoDialogue"));
				return;
			}

			if (result.Count == 0)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.NoDialogueIssues"));
				return;
			}

			this.Output.ShowTab(OutputTab.Errors);
		}

		/// <summary>Opens the dialogue function rules configuration.</summary>
		[RelayCommand]
		public async Task ShowFunctionRulesAsync()
		{
			await this._scriptTools.ShowFunctionRulesAsync();
		}

		/// <summary>Compiles the active script.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task CompileAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._build.CompileAsync(document);
				this.OnPropertyChanged(nameof(this.IsBuildRunning));
			}
		}

		/// <summary>Compiles every open script.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task CompileAllOpenAsync()
		{
			await this._build.CompileAllOpenAsync();
		}

		/// <summary>Compiles all scripts of a folder.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task MassCompileAsync()
		{
			await this._build.MassCompileAsync();
		}

		/// <summary>Preprocesses the active script and opens the result.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task PreprocessAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._build.PreprocessAsync(document);
			}
		}

		/// <summary>Compiles the active script and decompiles the result.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task RoundTripAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._build.RoundTripAsync(document);
			}
		}

		/// <summary>Parses the active script again right now.</summary>
		[RelayCommand]
		public async Task UpdateParserDataAsync()
		{
			if (this._workspace.ActiveDocument is { IsParsable: true } document)
			{
				await this._workspace.RequestParseAsync(document, true);
			}
		}

		/// <summary>Opens the settings dialog.</summary>
		[RelayCommand]
		public async Task SettingsAsync()
		{
			await this._toolWindows.ShowSettingsAsync();
			await this.AfterSettingsChangedAsync();
		}

		/// <summary>Opens the project folder in Explorer.</summary>
		[RelayCommand]
		public async Task OpenProjectFolderAsync()
		{
			String? folder = this._settings.Settings.Paths.SolutionFolder;
			if (folder is not null && Directory.Exists(folder))
			{
				this._shell.OpenFolder(folder);
			}
			else
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ProjectTitle"), Loc.Get("Str.Shell.Dlg.ProjectFolderMissing"));
			}
		}

		/// <summary>Applies the light, dark or system theme.</summary>
		[RelayCommand]
		public void SetTheme(AppTheme theme)
		{
			this._theme.Apply(theme);
			this._settings.Update(settings => settings.Editor.Theme = this._theme.IsDark ? HighlightTheme.Dark : HighlightTheme.Light);
			this.OnPropertyChanged(nameof(this.CurrentTheme));
			this.SaveLayout();
		}

		/// <summary>Shows or hides the left function tree.</summary>
		[RelayCommand]
		public void ToggleFunctionTree()
		{
			this.ShowFunctionTree = !this.ShowFunctionTree;
		}

		/// <summary>Shows or hides the right browser (procedures and variables).</summary>
		[RelayCommand]
		public void ToggleRightBrowser()
		{
			this.ShowRightBrowser = !this.ShowRightBrowser;
		}

		/// <summary>Shows or hides the log window.</summary>
		[RelayCommand]
		public void ToggleLog()
		{
			this.ShowLog = !this.ShowLog;
		}

		/// <summary>Collapses or restores the output panel.</summary>
		[RelayCommand]
		public void ToggleOutputMinimized()
		{
			this.IsOutputMinimized = !this.IsOutputMinimized;
		}

		/// <summary>Shows the About window.</summary>
		[RelayCommand]
		public void About()
		{
			this._toolWindows.ShowAbout();
		}

		/// <summary>The online documentation of sfall (the scripting functions, hooks and opcodes this editor is made for).</summary>
		public const String DocumentationUrl = "https://sfall-team.github.io/sfall/";

		/// <summary>Opens the sfall documentation site in the default browser.</summary>
		[RelayCommand]
		public async Task HelpAsync()
		{
			if (!this._shell.OpenWithShell(DocumentationUrl))
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Common.Help"), Loc.Format("Str.Shell.Dlg.DocsNotFound", DocumentationUrl));
			}
		}

		/// <summary>Associates the Fallout script file types (<c>.ssl .int .msg .fcd</c>) with the editor, after a confirmation (Help menu).</summary>
		[RelayCommand]
		public async Task AssociateFileTypesAsync()
		{
			String title = Loc.Get("Str.Shell.Dlg.AssociateTitle");
			if (!await this._dialogs.ConfirmAsync(title, Loc.Get("Str.Shell.Dlg.AssociateText"), Loc.Get("Str.Common.Yes"), Loc.Get("Str.Common.No")))
			{
				return;
			}

			try
			{
				this._association.Associate(Environment.ProcessPath ?? Path.Combine(this._settings.Paths.ProgramDirectory, "FalloutScriptStudio.exe"), this._settings.Paths.ResourcesDirectory);
			}
			catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
			{
				await this._dialogs.ShowErrorAsync(title, Loc.Format("Str.ToolWindows.Settings.AssociateFailed", ex.Message));
				return;
			}

			await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.ToolWindows.Settings.AssociateDone"));
		}

		private async Task<DocumentViewModel?> GetDialogueScriptAsync(String title)
		{
			if (this._workspace.ActiveDocument is not { } document)
			{
				return null;
			}

			if (document.Kind != DocumentKind.Script || document.FilePath is null)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.NoMessageFileKind"));
				return null;
			}

			if (document.ProgramInfo is null)
			{
				await this._workspace.RequestParseAsync(document, true);
			}

			String? message = await this._workspace.AssociateMessageFileAsync(document, false, true);
			if (message is null)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.MessageFileNotFound"));
				return null;
			}

			return document;
		}

		private void SetMessageEncoding(TextEncodingKind encoding)
		{
			this._settings.Update(settings => settings.Editor.FileEncoding = encoding);
			this.OnPropertyChanged(nameof(this.UseOemMessageEncoding));
			this.OnPropertyChanged(nameof(this.UseWindowsMessageEncoding));
		}

		private async Task AfterSettingsChangedAsync()
		{
			this.LoadPreprocessorDefines();
			this.LoadCompileProfiles();
			this.LoadTemplates();
			this.RefreshSettingsProperties();
			await this._workspace.ReloadGlobalMacrosAsync();
			foreach (DocumentViewModel document in this._workspace.Documents.Where(d => d.IsParsable))
			{
				_ = this._workspace.RequestParseAsync(document, false);
			}
		}
	}
}