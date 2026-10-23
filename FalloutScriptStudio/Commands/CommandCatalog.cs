namespace FalloutScriptStudio.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Windows.Input;
	using CommunityToolkit.Mvvm.Input;
	using Documents;
	using Editor;
	using Localization;
	using Services;
	using ViewModels.Main;

	/// <summary>
	/// The one registry of the commands of the application, for the command palette. <see cref="Definitions"/> is the table of the menu items of
	/// the main window (their place in the menus, the localization key of their header, the command of <see cref="MainViewModel"/> they run and
	/// when they are enabled); a test reads <c>MainWindow.xaml</c> and fails when a menu item is missing from the table or the table lists
	/// one that does not exist, so a new menu item cannot be forgotten. <see cref="Create"/> adds what has no menu item or changes at run time
	/// (the editor commands without a menu item, the recent files and projects, the templates, the languages, the compile profiles and the
	/// definitions) and binds everything to a view model. The names are never stored: they are read through <see cref="Loc"/> when asked.
	/// </summary>
	public static class CommandCatalog
	{
		/// <summary>The most recent files the palette lists.</summary>
		public const Int32 MaxRecentFiles = 20;

		private const String MenuFile = "Str.Shell.Palette.Menu.File";
		private const String MenuEdit = "Str.Shell.Palette.Menu.Edit";
		private const String MenuCode = "Str.Shell.Palette.Menu.Code";
		private const String MenuScript = "Str.Shell.Palette.Menu.Script";
		private const String MenuHeaders = "Str.Shell.Palette.Menu.Headers";
		private const String MenuDialog = "Str.Shell.Palette.Menu.Dialog";
		private const String MenuCompile = "Str.Shell.Palette.Menu.Compile";
		private const String MenuView = "Str.Shell.Palette.Menu.View";
		private const String MenuOptions = "Str.Shell.Palette.Menu.Options";
		private const String MenuHelp = "Str.Shell.Palette.Menu.Help";
		private const String MenuTheme = "Str.Common.Theme";
		private const String MenuEncoding = "Str.Shell.Menu.MessageEncoding";
		private const String MenuTemplates = "Str.Shell.Menu.NewFromTemplate";
		private const String MenuRecent = "Str.Shell.Menu.OpenRecent";
		private const String MenuRecentProjects = "Str.Shell.Menu.RecentProjects";
		private const String MenuLanguage = "Str.Common.LanguageTip";
		private const String MenuEditor = "Str.Shell.Palette.GroupEditor";
		private const String MenuProfile = "Str.Shell.Palette.GroupProfile";
		private const String MenuDefine = "Str.Shell.Palette.GroupDefine";

		private static readonly IReadOnlyList<CommandDefinition> _definitions = BuildDefinitions();
		private static readonly IReadOnlyList<CommandDefinition> _extraDefinitions = BuildExtraDefinitions();

		/// <summary>Gets the table of the menu items of the main window that run a command or change a setting.</summary>
		public static IReadOnlyList<CommandDefinition> Definitions => _definitions;

		/// <summary>Gets the commands that have no menu item of their own (the shortcut only commands, the "clear recent" items built in code).</summary>
		public static IReadOnlyList<CommandDefinition> ExtraDefinitions => _extraDefinitions;

		/// <summary>Builds the entries of the palette for a view model: the table, the editor commands, and the entries that depend on the state of the application.</summary>
		/// <param name="viewModel">The view model of the main window.</param>
		/// <returns>The entries in menu order; an entry appears once.</returns>
		public static IReadOnlyList<CommandEntry> Create(MainViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			List<CommandEntry> entries = [];
			HashSet<String> ids = new(StringComparer.Ordinal);
			void Add(CommandEntry entry)
			{
				if (ids.Add(entry.Id))
				{
					entries.Add(entry);
				}
			}

			foreach (CommandDefinition definition in _definitions.Concat(_extraDefinitions))
			{
				Add(CreateEntry(viewModel, definition));
			}

			AddTemplates(viewModel, Add);
			AddRecentFiles(viewModel, Add);
			AddRecentProjects(viewModel, Add);
			AddEditorCommands(viewModel, ids, Add);
			AddLanguages(viewModel, Add);
			AddCompileProfiles(viewModel, Add);
			AddDefinitions(viewModel, Add);
			return entries;
		}

		/// <summary>Finds the object a path such as <c>Output.NextProblemCommand</c> leads to, starting at the main view model.</summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="path">The properties to follow, separated by dots.</param>
		/// <returns>The object.</returns>
		/// <exception cref="InvalidOperationException">A property of the path does not exist.</exception>
		public static Object ResolvePath(MainViewModel viewModel, String path)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentException.ThrowIfNullOrEmpty(path);

			Object current = viewModel;
			foreach (String name in path.Split('.'))
			{
				PropertyInfo property = current.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
					?? throw new InvalidOperationException($"The property '{name}' of the path '{path}' does not exist on {current.GetType().Name}.");
				current = property.GetValue(current) ?? throw new InvalidOperationException($"The property '{name}' of the path '{path}' is null.");
			}

			return current;
		}

		private static CommandEntry CreateEntry(MainViewModel viewModel, CommandDefinition definition)
		{
			CommandText[] path = definition.MenuKeys.Select(CommandText.FromKey).ToArray();
			CommandText title = CommandText.FromKey(definition.TitleKey);
			if (definition.CommandPath is { } commandPath)
			{
				ICommand command = (ICommand)ResolvePath(viewModel, commandPath);
				Func<CommandAvailability> availability = () => Evaluate(viewModel, definition.Requirement, command, definition.Parameter);
				Func<Boolean>? isOn = definition.Parameter is AppTheme theme ? () => viewModel.CurrentTheme == theme : null;
				return new CommandEntry(definition.Id, path, title, command, definition.Parameter, availability, isOn, isOn is not null);
			}

			String property = definition.TogglePath ?? throw new InvalidOperationException("A definition needs a command or a property.");
			PropertyInfo info = typeof(MainViewModel).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
				?? throw new InvalidOperationException($"The property '{property}' does not exist on {nameof(MainViewModel)}.");
			Func<Boolean> read = () => (Boolean)info.GetValue(viewModel)!;
			RelayCommand toggle = new(() => info.SetValue(viewModel, !read()));
			return new CommandEntry(definition.Id, path, title, toggle, null, () => Evaluate(viewModel, definition.Requirement, toggle, null), read, false, property);
		}

		private static CommandAvailability Evaluate(MainViewModel viewModel, CommandRequirement requirement, ICommand command, Object? parameter)
		{
			CommandAvailability availability = requirement switch
			{
				CommandRequirement.Document => viewModel.HasDocument ? CommandAvailability.Available : CommandAvailability.NeedsDocument,
				CommandRequirement.Script => viewModel.IsScript ? CommandAvailability.Available : CommandAvailability.NeedsScript,
				CommandRequirement.Parsable => viewModel.IsParsable ? CommandAvailability.Available : CommandAvailability.NeedsParsable,
				CommandRequirement.Message => viewModel.IsMessage ? CommandAvailability.Available : CommandAvailability.NeedsMessage,
				CommandRequirement.Undo => viewModel.CanUndo ? CommandAvailability.Available : CommandAvailability.NothingToUndo,
				CommandRequirement.Redo => viewModel.CanRedo ? CommandAvailability.Available : CommandAvailability.NothingToRedo,
				CommandRequirement.GoBack => viewModel.CanGoBack ? CommandAvailability.Available : CommandAvailability.NoPreviousPosition,
				CommandRequirement.GoForward => viewModel.CanGoForward ? CommandAvailability.Available : CommandAvailability.NoNextPosition,
				CommandRequirement.Project => viewModel.HasProject ? CommandAvailability.Available : CommandAvailability.NeedsProject,
				CommandRequirement.UndoReplaceInFiles => viewModel.CanUndoReplaceInFiles ? CommandAvailability.Available : CommandAvailability.NothingToUndo,
				CommandRequirement.OldDecompiler => viewModel.CanUseOldDecompiler ? CommandAvailability.Available : CommandAvailability.NeedsExternalDecompiler,
				_ => CommandAvailability.Available,
			};
			if (availability != CommandAvailability.Available)
			{
				return availability;
			}

			if (parameter is EditorCommand editorCommand && !viewModel.CanEditorExecute(editorCommand))
			{
				return CommandAvailability.NotNow;
			}

			return command.CanExecute(parameter) ? CommandAvailability.Available : CommandAvailability.NotNow;
		}

		private static void AddTemplates(MainViewModel viewModel, Action<CommandEntry> add)
		{
			ICommand command = viewModel.NewFromTemplateCommand;
			foreach (TemplateItem template in viewModel.Templates)
			{
				add(new CommandEntry(
					"NewFromTemplateCommand:" + template.Path,
					new[] { CommandText.FromKey(MenuFile), CommandText.FromKey(MenuTemplates) },
					CommandText.FromLiteral(template.Name),
					command,
					template,
					() => Evaluate(viewModel, CommandRequirement.None, command, template)));
			}
		}

		private static void AddRecentFiles(MainViewModel viewModel, Action<CommandEntry> add)
		{
			ICommand command = viewModel.OpenRecentCommand;
			foreach (String file in viewModel.RecentFiles.Take(MaxRecentFiles))
			{
				add(new CommandEntry(
					"OpenRecentCommand:" + file,
					new[] { CommandText.FromKey(MenuFile), CommandText.FromKey(MenuRecent) },
					CommandText.FromLiteral(file),
					command,
					file,
					() => Evaluate(viewModel, CommandRequirement.None, command, file)));
			}
		}

		private static void AddRecentProjects(MainViewModel viewModel, Action<CommandEntry> add)
		{
			ICommand command = viewModel.OpenRecentProjectCommand;
			foreach (String folder in viewModel.RecentProjects)
			{
				add(new CommandEntry(
					"OpenRecentProjectCommand:" + folder,
					new[] { CommandText.FromKey(MenuFile), CommandText.FromKey(MenuRecentProjects) },
					CommandText.FromLiteral(folder),
					command,
					folder,
					() => Evaluate(viewModel, CommandRequirement.None, command, folder)));
			}
		}

		private static void AddEditorCommands(MainViewModel viewModel, HashSet<String> ids, Action<CommandEntry> add)
		{
			ICommand command = viewModel.EditCommand;
			foreach (KeyValuePair<EditorCommand, RoutedUICommand> pair in EditorCommands.All)
			{
				String id = "EditCommand:" + pair.Key;
				if (ids.Contains(id))
				{
					continue;
				}

				EditorCommand editorCommand = pair.Key;
				String key = EditorCommands.GetTextKey(pair.Value) ?? "Str.Editor.Cmd." + editorCommand;
				add(new CommandEntry(
					id,
					new[] { CommandText.FromKey(MenuEditor) },
					CommandText.FromKey(key),
					command,
					editorCommand,
					() => Evaluate(viewModel, CommandRequirement.Document, command, editorCommand)));
			}
		}

		private static void AddLanguages(MainViewModel viewModel, Action<CommandEntry> add)
		{
			foreach (LanguageInfo language in Loc.Languages)
			{
				String code = language.Code;
				RelayCommand choose = new(() => viewModel.ChangeLanguage(code));
				add(new CommandEntry(
					"language:" + code,
					new[] { CommandText.FromKey(MenuView), CommandText.FromKey(MenuLanguage) },
					CommandText.FromLiteral(language.NativeName),
					choose,
					null,
					() => CommandAvailability.Available,
					() => String.Equals(Loc.Language, code, StringComparison.Ordinal),
					true));
			}
		}

		private static void AddCompileProfiles(MainViewModel viewModel, Action<CommandEntry> add)
		{
			foreach (CompileProfileOption option in viewModel.CompileProfileOptions)
			{
				String name = option.Value;
				RelayCommand choose = new(() => viewModel.SelectedCompileProfile = name);
				add(new CommandEntry(
					"profile:" + name,
					new[] { CommandText.FromKey(MenuCompile), CommandText.FromKey(MenuProfile) },
					name.Length == 0 ? CommandText.FromKey("Str.Shell.Palette.NoProfile") : CommandText.FromLiteral(name),
					choose,
					null,
					() => CommandAvailability.Available,
					() => String.Equals(viewModel.SelectedCompileProfile, name, StringComparison.OrdinalIgnoreCase),
					true));
			}
		}

		private static void AddDefinitions(MainViewModel viewModel, Action<CommandEntry> add)
		{
			foreach (PreprocessorDefineOption option in viewModel.PreprocessorDefineOptions)
			{
				String value = option.Value;
				RelayCommand choose = new(() => viewModel.SelectedPreprocessorDefine = value);
				add(new CommandEntry(
					"define:" + value,
					new[] { CommandText.FromKey(MenuCompile), CommandText.FromKey(MenuDefine) },
					value.Length == 0 ? CommandText.FromKey("Str.Shell.Palette.NoDefine") : CommandText.FromLiteral(value),
					choose,
					null,
					() => viewModel.CanChooseDefine ? CommandAvailability.Available : CommandAvailability.NotNow,
					() => String.Equals(viewModel.SelectedPreprocessorDefine, value, StringComparison.Ordinal),
					true));
			}
		}

		private static List<CommandDefinition> BuildExtraDefinitions()
		{
			List<CommandDefinition> list = [];
			void Command(String menu, String title, String command, CommandRequirement requirement = CommandRequirement.None)
			{
				list.Add(new CommandDefinition(new[] { menu }, title, command, null, requirement));
			}

			Command(MenuFile, "Str.Shell.Menu.ClearRecent", "ClearRecentCommand");
			Command(MenuFile, "Str.Shell.Menu.ClearRecentProjects", "ClearRecentProjectsCommand");
			Command(MenuView, "Str.Shell.Palette.NextDocument", "NextDocumentCommand", CommandRequirement.Document);
			Command(MenuView, "Str.Shell.Palette.PreviousDocument", "PreviousDocumentCommand", CommandRequirement.Document);
			Command(MenuCode, "Str.Panels.MenuCreateProcedure", "Procedures.CreateProcedureCommand", CommandRequirement.Parsable);
			return list;
		}

		private static List<CommandDefinition> BuildDefinitions()
		{
			List<CommandDefinition> list = [];
			void Command(String menu, String title, String command, CommandRequirement requirement = CommandRequirement.None)
			{
				list.Add(new CommandDefinition(new[] { menu }, title, command, null, requirement));
			}

			void Edit(String title, EditorCommand command, CommandRequirement requirement, String menu = MenuCode)
			{
				list.Add(new CommandDefinition(new[] { menu }, title, "EditCommand", command, requirement));
			}

			void Toggle(String menu, String title, String property, CommandRequirement requirement = CommandRequirement.None, String? submenu = null)
			{
				list.Add(new CommandDefinition(submenu is null ? new[] { menu } : new[] { menu, submenu }, title, null, null, requirement, property));
			}

			// File
			Command(MenuFile, "Str.Shell.Menu.NewScript", "NewScriptCommand");
			Command(MenuFile, "Str.Shell.Menu.Open", "OpenCommand");
			Command(MenuFile, "Str.Shell.Menu.OpenFromArchive", "OpenFromArchiveCommand");
			Command(MenuFile, "Str.Shell.Menu.OpenProject", "OpenProjectCommand");
			Command(MenuFile, "Str.Shell.Menu.CreateProjectSettings", "CreateProjectSettingsCommand");
			Command(MenuFile, "Str.Shell.Menu.CloseProject", "CloseProjectCommand");
			Command(MenuFile, "Str.Shell.Menu.ShowProjectFolder", "OpenProjectFolderCommand", CommandRequirement.Project);
			Command(MenuFile, "Str.Common.Save", "SaveCommand", CommandRequirement.Document);
			Command(MenuFile, "Str.Shell.Menu.SaveAll", "SaveAllCommand", CommandRequirement.Document);
			Command(MenuFile, "Str.Shell.Menu.SaveAs", "SaveAsCommand", CommandRequirement.Document);
			Command(MenuFile, "Str.Shell.Menu.SaveAsTemplate", "SaveAsTemplateCommand", CommandRequirement.Script);
			Toggle(MenuFile, "Str.Shell.Menu.SaveUtf8", "SaveAsUtf8");
			Command(MenuFile, "Str.Common.Close", "CloseDocumentCommand", CommandRequirement.Document);
			Command(MenuFile, "Str.Shell.Menu.CloseAll", "CloseAllCommand", CommandRequirement.Document);
			Command(MenuFile, "Str.Shell.Menu.Exit", "ExitCommand");

			// Edit
			Command(MenuEdit, "Str.Common.Undo", "UndoCommand", CommandRequirement.Undo);
			Command(MenuEdit, "Str.Common.Redo", "RedoCommand", CommandRequirement.Redo);
			Command(MenuEdit, "Str.Common.Cut", "CutCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Common.Copy", "CopyCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Common.Paste", "PasteCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Common.SelectAll", "SelectAllCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Shell.Menu.FindInFiles", "AdvancedSearchCommand");
			Command(MenuEdit, "Str.Shell.Menu.ReplaceInFiles", "ReplaceInFilesCommand");
			Command(MenuEdit, "Str.Shell.Menu.UndoReplaceInFiles", "UndoReplaceInFilesCommand", CommandRequirement.UndoReplaceInFiles);
			Command(MenuEdit, "Str.Shell.Menu.FindReplaceBar", "ShowSearchBarCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Shell.Menu.QuickFind", "QuickFindCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Shell.Menu.FindNext", "FindNextCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Shell.Menu.FindPrevious", "FindPreviousCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Shell.Menu.GoToLine", "GoToLineCommand", CommandRequirement.Document);
			Command(MenuEdit, "Str.Shell.Menu.GoToProcedure", "GoToProcedureCommand", CommandRequirement.Parsable);
			Command(MenuEdit, "Str.Shell.Menu.GoToSymbol", "GoToSymbolCommand");
			Command(MenuEdit, "Str.Shell.Menu.Back", "GoBackCommand", CommandRequirement.GoBack);
			Command(MenuEdit, "Str.Shell.Menu.Forward", "GoForwardCommand", CommandRequirement.GoForward);
			Edit("Str.Shell.Menu.SplitDocument", EditorCommand.SplitDocument, CommandRequirement.Document, MenuEdit);

			// Code
			Edit("Str.Shell.Menu.CommentText", EditorCommand.CommentSelection, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.UncommentText", EditorCommand.UncommentSelection, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.ToggleLineComment", EditorCommand.ToggleLineComment, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.ToggleRegionComment", EditorCommand.ToggleBlockComment, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.AlignSelectedText", EditorCommand.AlignSelectionLeft, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.DecreaseIndent", EditorCommand.DecreaseIndent, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.IncreaseIndent", EditorCommand.IncreaseIndent, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.FormatDocument", EditorCommand.FormatDocument, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.FormatSelection", EditorCommand.FormatSelection, CommandRequirement.Parsable);
			Command(MenuCode, "Str.Shell.Menu.FormatProject", "FormatProjectCommand");
			Edit("Str.Shell.Menu.UpperCase", EditorCommand.UpperCase, CommandRequirement.Document);
			Edit("Str.Shell.Menu.LowerCase", EditorCommand.LowerCase, CommandRequirement.Document);
			Edit("Str.Shell.Menu.CapitalizeWord", EditorCommand.CapitalizeWord, CommandRequirement.Document);
			Edit("Str.Shell.Menu.ConvertHexDec", EditorCommand.ConvertHexDec, CommandRequirement.Document);
			Edit("Str.Shell.Menu.ConvertLeadingTabsToSpaces", EditorCommand.ConvertLeadingTabsToSpaces, CommandRequirement.Document);
			Edit("Str.Shell.Menu.ConvertAllTabsToSpaces", EditorCommand.ConvertAllTabsToSpaces, CommandRequirement.Document);
			Edit("Str.Shell.Menu.ConvertLeadingSpacesToTabs", EditorCommand.ConvertLeadingSpacesToTabs, CommandRequirement.Document);
			Edit("Str.Shell.Menu.TrimTrailingSpaces", EditorCommand.TrimTrailingSpaces, CommandRequirement.Document);
			Toggle(MenuCode, "Str.Shell.Menu.AutoTrimTrailing", "AutoTrimTrailingSpaces");
			Toggle(MenuCode, "Str.Shell.Menu.AutoFormatCode", "FormatCodeOnOpen");
			Toggle(MenuCode, "Str.Shell.Menu.ShowWhitespace", "ShowWhitespace");
			Edit("Str.Shell.Menu.HighlightWord", EditorCommand.HighlightWord, CommandRequirement.Document);
			Edit("Str.Shell.Menu.Autocomplete", EditorCommand.ShowCompletion, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.InsertSnippet", EditorCommand.InsertSnippet, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.SurroundWith", EditorCommand.SurroundWith, CommandRequirement.Parsable);
			Command(MenuCode, "Str.Shell.Menu.ManageSnippets", "ManageSnippetsCommand");
			Edit("Str.Shell.Menu.ToggleOutline", EditorCommand.ToggleOutline, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.CollapseAll", EditorCommand.CollapseAll, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.ExpandAll", EditorCommand.ExpandAll, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.GoToDeclaration", EditorCommand.GoToDeclaration, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.GoToDefinition", EditorCommand.GoToDefinition, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.FindReferences", EditorCommand.FindReferences, CommandRequirement.Parsable);
			Edit("Str.Common.Rename", EditorCommand.RenameSymbol, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.OpenInclude", EditorCommand.OpenInclude, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.QuickFix", EditorCommand.QuickFix, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.ExtractProcedure", EditorCommand.ExtractProcedure, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.DeleteUnusedProcedure", EditorCommand.DeleteUnusedProcedure, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.FindUnusedVariables", EditorCommand.FindUnusedVariables, CommandRequirement.Parsable);
			Edit("Str.Shell.Menu.RemoveUnusedVariable", EditorCommand.RemoveUnusedVariable, CommandRequirement.Parsable);
			Command(MenuCode, "Str.Shell.Menu.CheckGlobalVariables", "CheckGlobalVariablesCommand");
			Edit("Str.Shell.Menu.FunctionHelp", EditorCommand.ShowHelp, CommandRequirement.Parsable);
			Command(MenuCode, "Str.Shell.Menu.CheckMessageStructure", "CheckMessageStructureCommand", CommandRequirement.Message);

			// Script
			Command(MenuScript, "Str.Shell.Menu.ScriptsListEditor", "ScriptsListCommand");
			Command(MenuScript, "Str.Shell.Menu.RegisterScript", "RegisterScriptCommand", CommandRequirement.Script);
			Toggle(MenuScript, "Str.Shell.Menu.DefineToScriptsHeader", "DefineToScriptsHeader");

			// Headers
			Command(MenuHeaders, "Str.Shell.Menu.IncludeFile", "IncludeFileToCodeCommand", CommandRequirement.Parsable);
			Command(MenuHeaders, "Str.Shell.Menu.OpenHeader", "OpenHeaderFileCommand");
			Command(MenuHeaders, "Str.Shell.Menu.BrowseHeaders", "BrowseHeadersCommand");
			Command(MenuHeaders, "Str.Shell.Menu.OpenAllIncludes", "OpenAllIncludesCommand", CommandRequirement.Parsable);

			// Dialogue
			Command(MenuDialog, "Str.Shell.Menu.MessageEditor", "OpenMessageEditorCommand");
			Command(MenuDialog, "Str.Shell.Menu.AssociatedMessageFile", "AssociateMessageFileCommand", CommandRequirement.Script);
			Command(MenuDialog, "Str.Shell.Menu.NodesFlowchart", "ShowFlowchartCommand", CommandRequirement.Script);
			Command(MenuDialog, "Str.Shell.Menu.DialoguePreview", "ShowDialoguePreviewCommand", CommandRequirement.Script);
			Edit("Str.Shell.Menu.PreviewDialogueFromNode", EditorCommand.PreviewDialogueFromNode, CommandRequirement.Parsable, MenuDialog);
			Command(MenuDialog, "Str.Shell.Menu.CheckMessages", "CheckMessagesCommand", CommandRequirement.Script);
			Command(MenuDialog, "Str.Shell.Menu.CheckDialogue", "CheckDialogueCommand", CommandRequirement.Script);
			Command(MenuDialog, "Str.Shell.Menu.CompareMessageFiles", "CompareMessageFilesCommand");
			Command(MenuDialog, "Str.Shell.Menu.FunctionsConfig", "ShowFunctionRulesCommand");
			Toggle(MenuDialog, "Str.Shell.Menu.AutoOpenMessageEditor", "AutoOpenMessageEditor");

			// Compile
			Command(MenuCompile, "Str.Shell.Menu.CompileScript", "CompileCommand", CommandRequirement.Script);
			Command(MenuCompile, "Str.Shell.Menu.CompileAllOpen", "CompileAllOpenCommand", CommandRequirement.Document);
			Command(MenuCompile, "Str.Shell.Menu.BuildChanged", "BuildChangedCommand", CommandRequirement.Document);
			Command(MenuCompile, "Str.Shell.Menu.MassCompile", "MassCompileCommand");
			Command(MenuCompile, "Str.Shell.Menu.RebuildAll", "RebuildAllCommand");
			Toggle(MenuCompile, "Str.Shell.Menu.MassCompileOnlyChanged", "MassCompileOnlyChanged");
			Command(MenuCompile, "Str.Shell.Menu.Preprocess", "PreprocessCommand", CommandRequirement.Script);
			Command(MenuCompile, "Str.Shell.Menu.Roundtrip", "RoundTripCommand", CommandRequirement.Script);
			Command(MenuCompile, "Str.Shell.Operation.Cancel", "CancelOperationCommand");
			Command(MenuCompile, "Str.Shell.Problems.Show", "ShowProblemsCommand");
			Command(MenuCompile, "Str.Shell.Problems.Next", "Output.NextProblemCommand");
			Command(MenuCompile, "Str.Shell.Problems.Previous", "Output.PreviousProblemCommand");
			Toggle(MenuCompile, "Str.Shell.Menu.DecompileFallout1", "DecompileAsFallout1");
			Toggle(MenuCompile, "Str.Shell.Menu.UseOldDecompiler", "UseOldDecompiler", CommandRequirement.OldDecompiler);

			// View
			Toggle(MenuView, "Str.Shell.Menu.LineNumbers", "ShowLineNumbers");
			Toggle(MenuView, "Str.Shell.Menu.ShowIndentLine", "ShowIndentLine");
			Toggle(MenuView, "Str.Shell.Menu.HighlightParseErrors", "HighlightParseErrors");
			Toggle(MenuView, "Str.Shell.Menu.RightBrowser", "ShowRightBrowser");
			Toggle(MenuView, "Str.Shell.Menu.FunctionTree", "ShowFunctionTree");
			Toggle(MenuView, "Str.Shell.Menu.LogWindow", "ShowLog");
			Command(MenuView, "Str.Shell.Menu.ShowGlobalVariables", "ShowGlobalVariablesCommand");
			Command(MenuView, "Str.Shell.Menu.ShowHelpPanel", "ShowHelpPanelCommand");
			foreach (AppTheme theme in new[] { AppTheme.System, AppTheme.Light, AppTheme.Dark })
			{
				list.Add(new CommandDefinition(new[] { MenuView, MenuTheme }, "Str.Common.Theme" + theme, "SetThemeCommand", theme, CommandRequirement.None));
			}

			Command(MenuView, "Str.Shell.Menu.ZoomIn", "ZoomInCommand", CommandRequirement.Document);
			Command(MenuView, "Str.Shell.Menu.ZoomOut", "ZoomOutCommand", CommandRequirement.Document);
			Command(MenuView, "Str.Shell.Menu.ResetZoom", "ResetZoomCommand", CommandRequirement.Document);
			Command(MenuView, "Str.Shell.Menu.CommandPalette", "ShowCommandPaletteCommand");

			// Options
			Command(MenuOptions, "Str.Shell.Menu.Settings", "SettingsCommand");
			Command(MenuOptions, "Str.Shell.Menu.ProjectSettings", "ProjectSettingsCommand");
			Toggle(MenuOptions, "Str.Shell.Menu.EncodingWindows", "UseWindowsMessageEncoding", CommandRequirement.None, MenuEncoding);
			Toggle(MenuOptions, "Str.Shell.Menu.EncodingOem", "UseOemMessageEncoding", CommandRequirement.None, MenuEncoding);

			// Help
			Command(MenuHelp, "Str.Shell.Menu.Documentation", "HelpCommand");
			Command(MenuHelp, "Str.Shell.Menu.AssociateFiles", "AssociateFileTypesCommand");
			Command(MenuHelp, "Str.Shell.Menu.About", "AboutCommand");
			return list;
		}
	}
}