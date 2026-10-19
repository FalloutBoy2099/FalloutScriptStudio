namespace FalloutScriptStudio.Tests.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using FalloutScriptStudio.Commands;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Accessibility;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>
	/// The catalog of the command palette against the menu of the main window (read statically from <c>MainWindow.xaml</c>): a menu item that
	/// is added, removed or changed without the catalog being updated fails here, so the palette cannot forget a command.
	/// </summary>
	public sealed class CommandCatalogTests
	{
		private static readonly XNamespace Presentation = SourceTree.Presentation;
		private static readonly Regex BindingPath = new Regex(@"^\{Binding\s+(?<path>[\w.]+)", RegexOptions.Compiled);
		private static readonly Regex ResourceKey = new Regex(@"^\{DynamicResource\s+(?<key>[\w.]+)\}$", RegexOptions.Compiled);
		private static readonly Regex StaticMember = new Regex(@"^\{x:Static\s+\w+:(?<type>\w+)\.(?<member>\w+)\}$", RegexOptions.Compiled);

		/// <summary>The commands that only a key binding of the window runs, and the check item (a property of the view model) that does the same thing in the palette.</summary>
		private static readonly Dictionary<String, String> ToggleAliases = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["ToggleFunctionTreeCommand"] = "ShowFunctionTree",
			["ToggleRightBrowserCommand"] = "ShowRightBrowser",
			["ToggleLogCommand"] = "ShowLog",
		};

		/// <summary>What a menu item of the window says about when it is enabled, as the requirement of the catalog.</summary>
		private static CommandRequirement ReadRequirement(XElement item)
		{
			String style = (String?)item.Attribute("Style") ?? String.Empty;
			if (style.Contains("DocumentMenuItemStyle", StringComparison.Ordinal))
			{
				return CommandRequirement.Document;
			}

			if (style.Contains("ScriptMenuItemStyle", StringComparison.Ordinal))
			{
				return CommandRequirement.Script;
			}

			if (style.Contains("ParsableMenuItemStyle", StringComparison.Ordinal))
			{
				return CommandRequirement.Parsable;
			}

			Match enabled = BindingPath.Match((String?)item.Attribute("IsEnabled") ?? String.Empty);
			return !enabled.Success
				? CommandRequirement.None
				: enabled.Groups["path"].Value switch
				{
					"HasDocument" => CommandRequirement.Document,
					"IsScript" => CommandRequirement.Script,
					"IsParsable" => CommandRequirement.Parsable,
					"IsMessage" => CommandRequirement.Message,
					"CanUndo" => CommandRequirement.Undo,
					"CanRedo" => CommandRequirement.Redo,
					"CanGoBack" => CommandRequirement.GoBack,
					"CanGoForward" => CommandRequirement.GoForward,
					"HasProject" => CommandRequirement.Project,
					"CanUndoReplaceInFiles" => CommandRequirement.UndoReplaceInFiles,
					"CanUseOldDecompiler" => CommandRequirement.OldDecompiler,
					String other => throw new InvalidOperationException("The menu item is enabled by '" + other + "': teach the catalog test this condition."),
				};
		}

		private static XDocument LoadWindow()
		{
			return XDocument.Load(System.IO.Path.Combine(SourceTree.AppFolder, "Views", "Shell", "MainWindow.xaml"));
		}

		/// <summary>
		/// The localization key of the header of a menu item, whether it is written as an attribute or as an element. The headers of the menu bar
		/// carry an access key marker, so the catalog uses the plain texts <c>Str.Shell.Palette.Menu.*</c> for them.
		/// </summary>
		private static String? HeaderKey(XElement item)
		{
			String? text = (String?)item.Attribute("Header");
			if (text is null && item.Element(Presentation + "MenuItem.Header") is { } header)
			{
				text = header.Descendants().Select(element => (String?)element.Attribute("Text") ?? (String?)element.Attribute("Content")).FirstOrDefault(value => value is not null);
			}

			Match match = ResourceKey.Match(text ?? String.Empty);
			return match.Success ? match.Groups["key"].Value.Replace("Str.Shell.MenuBar.", "Str.Shell.Palette.Menu.", StringComparison.Ordinal) : null;
		}

		/// <summary>The menu items of the window's menu bar that run a command or change a setting, with their menu path.</summary>
		private static List<(XElement Item, String[] MenuKeys, String TitleKey)> ReadMenuItems()
		{
			XElement menu = LoadWindow().Descendants(Presentation + "Menu").Single(element => (String?)element.Attribute(SourceTree.Xaml + "Name") == "MainMenu");
			List<(XElement, String[], String)> items = [];
			foreach (XElement item in menu.Descendants(Presentation + "MenuItem"))
			{
				Boolean hasCommand = item.Attribute("Command") is not null;
				Boolean isToggle = (String?)item.Attribute("IsCheckable") == "True" && item.Attribute("IsChecked") is not null;
				if (!hasCommand && !isToggle)
				{
					continue;
				}

				String[] keys = item.Ancestors(Presentation + "MenuItem").Reverse().Select(parent => HeaderKey(parent) ?? throw new InvalidOperationException("A menu has no header key.")).ToArray();
				items.Add((item, keys, HeaderKey(item) ?? throw new InvalidOperationException("A menu item has no header key.")));
			}

			return items;
		}

		private static String ParameterText(XElement item)
		{
			String? parameter = (String?)item.Attribute("CommandParameter");
			if (parameter is null)
			{
				return String.Empty;
			}

			Match match = StaticMember.Match(parameter);
			Assert.True(match.Success, "Unsupported command parameter: " + parameter);
			return ":" + match.Groups["type"].Value + "." + match.Groups["member"].Value;
		}

		private static String KeyOf(CommandDefinition definition)
		{
			String parameter = definition.Parameter is null ? String.Empty : ":" + definition.Parameter.GetType().Name + "." + definition.Parameter;
			return (definition.CommandPath ?? "toggle:" + definition.TogglePath) + parameter;
		}

		[Fact]
		public void EveryMenuItemOfTheWindowIsInTheCatalog_WithTheSamePathHeaderAndCondition()
		{
			Dictionary<String, CommandDefinition> catalog = CommandCatalog.Definitions.ToDictionary(KeyOf, StringComparer.Ordinal);
			List<String> problems = [];

			foreach ((XElement item, String[] menuKeys, String titleKey) in ReadMenuItems())
			{
				String key;
				if (item.Attribute("Command") is { } command)
				{
					key = BindingPath.Match(command.Value).Groups["path"].Value + ParameterText(item);
				}
				else
				{
					key = "toggle:" + BindingPath.Match((String)item.Attribute("IsChecked")!).Groups["path"].Value;
				}

				if (!catalog.TryGetValue(key, out CommandDefinition? definition))
				{
					problems.Add("Not in the catalog: " + key + " (" + titleKey + "). Add it to CommandCatalog.BuildDefinitions.");
					continue;
				}

				if (!definition.MenuKeys.SequenceEqual(menuKeys))
				{
					problems.Add(key + ": menu " + String.Join(" > ", definition.MenuKeys) + " but the window has " + String.Join(" > ", menuKeys));
				}

				if (!String.Equals(definition.TitleKey, titleKey, StringComparison.Ordinal))
				{
					problems.Add(key + ": header " + definition.TitleKey + " but the window has " + titleKey);
				}

				CommandRequirement requirement = ReadRequirement(item);
				if (definition.Requirement != requirement)
				{
					problems.Add(key + ": requirement " + definition.Requirement + " but the window enables the item for " + requirement);
				}
			}

			Assert.True(problems.Count == 0, Environment.NewLine + String.Join(Environment.NewLine, problems));
		}

		[Fact]
		public void EveryDefinitionOfTheCatalogIsAMenuItemOfTheWindow()
		{
			HashSet<String> inWindow = new(StringComparer.Ordinal);
			foreach ((XElement item, _, _) in ReadMenuItems())
			{
				inWindow.Add(item.Attribute("Command") is { } command
					? BindingPath.Match(command.Value).Groups["path"].Value + ParameterText(item)
					: "toggle:" + BindingPath.Match((String)item.Attribute("IsChecked")!).Groups["path"].Value);
			}

			String[] orphans = CommandCatalog.Definitions.Select(KeyOf).Where(key => !inWindow.Contains(key)).ToArray();

			Assert.True(orphans.Length == 0, "The catalog lists commands that no menu item runs (move them to ExtraDefinitions or delete them): " + String.Join(", ", orphans));
		}

		[Fact]
		public void EveryKeyBindingOfTheWindowRunsACommandOfTheCatalog()
		{
			HashSet<String> known = new HashSet<String>(CommandCatalog.Definitions.Concat(CommandCatalog.ExtraDefinitions).Where(d => d.CommandPath is not null).Select(d => d.CommandPath!), StringComparer.Ordinal);
			HashSet<String> toggles = new HashSet<String>(CommandCatalog.Definitions.Where(d => d.TogglePath is not null).Select(d => d.TogglePath!), StringComparer.Ordinal);
			List<String> missing = [];

			foreach (XElement binding in LoadWindow().Descendants(Presentation + "KeyBinding"))
			{
				String path = BindingPath.Match((String)binding.Attribute("Command")!).Groups["path"].Value;
				Boolean covered = known.Contains(path) || (ToggleAliases.TryGetValue(path, out String? property) && toggles.Contains(property));
				if (!covered)
				{
					missing.Add(path);
				}
			}

			Assert.True(missing.Count == 0, "These key bindings run commands that the palette does not list: " + String.Join(", ", missing));
		}

		[Fact]
		public void ThePlainMenuNamesAreTheMenuBarTextsWithoutTheAccessKey_InEveryLanguage()
		{
			List<String> problems = [];
			foreach (String language in SourceTree.LanguageCodes())
			{
				Dictionary<String, String> strings = SourceTree.LoadStrings(language);
				foreach (String name in new[] { "File", "Edit", "Code", "Script", "Headers", "Dialog", "Compile", "View", "Options", "Help" })
				{
					String expected = MenuText.Clean(strings["Str.Shell.MenuBar." + name]);
					if (!String.Equals(expected, strings["Str.Shell.Palette.Menu." + name], StringComparison.Ordinal))
					{
						problems.Add(language + ": Str.Shell.Palette.Menu." + name + " should be '" + expected + "'");
					}
				}
			}

			Assert.True(problems.Count == 0, Environment.NewLine + String.Join(Environment.NewLine, problems));
		}

		[Fact]
		public void EveryTextKeyOfTheCatalogExists()
		{
			Dictionary<String, String> english = SourceTree.LoadStrings("en");
			List<String> missing = [];
			foreach (CommandDefinition definition in CommandCatalog.Definitions.Concat(CommandCatalog.ExtraDefinitions))
			{
				foreach (String key in definition.MenuKeys.Append(definition.TitleKey).Where(key => !english.ContainsKey(key)))
				{
					missing.Add(key);
				}
			}

			Assert.True(missing.Count == 0, "Missing texts: " + String.Join(", ", missing.Distinct()));
		}

		[Fact]
		public void TheCatalogBindsToTheViewModel_EveryIdIsUnique_EveryNameIsReadable()
		{
			using ShellHarness harness = new ShellHarness();

			IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);

			Assert.Equal(entries.Count, entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
			Assert.True(entries.Count > 120, "Only " + entries.Count + " commands.");
			foreach (CommandEntry entry in entries)
			{
				Assert.False(String.IsNullOrWhiteSpace(entry.Title), entry.Id);
				Assert.DoesNotContain("Str.", entry.FullName, StringComparison.Ordinal);
				Assert.DoesNotContain("_", entry.Path.Replace("__", String.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
				Assert.False(entry.Title.EndsWith("...", StringComparison.Ordinal), entry.Id);
			}
		}

		[Fact]
		public void TheNameIsTheMenuPathAndTheHeaderWithoutAccessKeyAndEllipsis()
		{
			using ShellHarness harness = new ShellHarness();
			IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);

			Assert.Equal("Code > Format Document", entries.Single(entry => entry.Id == "EditCommand:FormatDocument").FullName);
			Assert.Equal("File > Open", entries.Single(entry => entry.Id == "OpenCommand").FullName);
			Assert.Equal("Edit > Find in Files", entries.Single(entry => entry.Id == "AdvancedSearchCommand").FullName);
			Assert.Equal("View > Theme > Light", entries.Single(entry => entry.Id == "SetThemeCommand:Light").FullName);
			Assert.Equal("Options > Encoding of Message Files > Windows Default", entries.Single(entry => entry.Id == "toggle:UseWindowsMessageEncoding").FullName);
		}

		[Fact]
		public void EveryEditorCommandIsInThePalette_TheOnesWithoutAMenuItemUnderTheEditorGroup()
		{
			using ShellHarness harness = new ShellHarness();
			IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);

			foreach (KeyValuePair<EditorCommand, System.Windows.Input.RoutedUICommand> pair in EditorCommands.All)
			{
				Assert.Contains(entries, entry => entry.Id == "EditCommand:" + pair.Key);
			}

			CommandEntry deleteLine = entries.Single(entry => entry.Id == "EditCommand:DeleteLine");
			Assert.Equal("Editor > Delete line", deleteLine.FullName);
		}

		[Fact]
		public void TheStateFollowsTheDocument_AndTheReasonIsKnown()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				CommandEntry Find(String id) => CommandCatalog.Create(harness.Main).Single(entry => entry.Id == id);

				Assert.Equal(CommandAvailability.Available, Find("NewScriptCommand").Availability);
				Assert.Equal(CommandAvailability.NeedsDocument, Find("SaveCommand").Availability);
				Assert.Equal(CommandAvailability.NeedsParsable, Find("EditCommand:FormatDocument").Availability);
				Assert.Equal(CommandAvailability.NeedsScript, Find("CompileCommand").Availability);
				Assert.Equal(CommandAvailability.NeedsMessage, Find("CheckMessageStructureCommand").Availability);
				Assert.Equal(CommandAvailability.NeedsProject, Find("OpenProjectFolderCommand").Availability);
				Assert.Equal(CommandAvailability.NotNow, Find("CloseProjectCommand").Availability);
				Assert.Equal(CommandAvailability.NothingToUndo, Find("UndoCommand").Availability);

				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\nend\r\n");
				harness.AttachEditor(document);

				Assert.Equal(CommandAvailability.Available, Find("SaveCommand").Availability);
				Assert.Equal(CommandAvailability.Available, Find("EditCommand:FormatDocument").Availability);
				Assert.Equal(CommandAvailability.Available, Find("CompileCommand").Availability);
				Assert.Equal(CommandAvailability.NeedsMessage, Find("CheckMessageStructureCommand").Availability);
			});
		}

		[Fact]
		public void TheCatalogAgreesWithTheEnabledStateOfTheMenu_ForEveryCondition()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel message = await harness.OpenFileAsync("a.msg", "{100}{}{x}\r\n");
				harness.AttachEditor(message);

				IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);

				// A message file is a document but not a script and not parsed.
				Assert.True(entries.Single(entry => entry.Id == "SaveCommand").CanRun);
				Assert.False(entries.Single(entry => entry.Id == "CompileCommand").CanRun);
				Assert.False(entries.Single(entry => entry.Id == "EditCommand:FormatDocument").CanRun);
				Assert.True(entries.Single(entry => entry.Id == "CheckMessageStructureCommand").CanRun);
				Assert.True(entries.Single(entry => entry.Id == "EditCommand:UpperCase").CanRun);
			});
		}

		[Fact]
		public void ACheckItemShowsAndChangesTheSetting()
		{
			using ShellHarness harness = new ShellHarness();
			CommandEntry entry = CommandCatalog.Create(harness.Main).Single(item => item.Id == "toggle:SaveAsUtf8");
			Boolean before = harness.Main.SaveAsUtf8;

			Assert.Equal(CommandStateKind.Toggle, entry.StateKind);
			Assert.Equal(before, entry.IsOn);
			entry.Command.Execute(entry.Parameter);

			Assert.Equal(!before, harness.Main.SaveAsUtf8);
			Assert.Equal(!before, entry.IsOn);
			Assert.Equal("SaveAsUtf8", entry.Property);
		}

		[Fact]
		public void TheThemesAreChoices_AndTheCurrentOneIsMarked()
		{
			using ShellHarness harness = new ShellHarness();
			IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);
			CommandEntry dark = entries.Single(entry => entry.Id == "SetThemeCommand:Dark");

			Assert.Equal(CommandStateKind.Choice, dark.StateKind);
			dark.Command.Execute(dark.Parameter);

			Assert.True(dark.IsOn);
			Assert.False(entries.Single(entry => entry.Id == "SetThemeCommand:Light").IsOn);
		}

		[Fact]
		public void TheLanguagesTheProfilesAndTheDefinitionsAreEntries()
		{
			using ShellHarness harness = new ShellHarness();
			IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);

			Assert.Equal(FalloutScriptStudio.Localization.Loc.Languages.Count, entries.Count(entry => entry.Id.StartsWith("language:", StringComparison.Ordinal)));
			Assert.Equal("View > Language > Français", entries.Single(entry => entry.Id == "language:fr").FullName);
			Assert.Contains(entries, entry => entry.Id == "profile:");
			Assert.Contains(entries, entry => entry.Id == "profile:Debug");
			Assert.Contains(entries, entry => entry.Id == "profile:Release");
			Assert.Contains(entries, entry => entry.Id == "define:");
			Assert.Equal("Compile > Compile profile > Debug", entries.Single(entry => entry.Id == "profile:Debug").FullName);

			CommandEntry release = entries.Single(entry => entry.Id == "profile:Release");
			release.Command.Execute(release.Parameter);
			Assert.Equal("Release", harness.Main.SelectedCompileProfile);
			Assert.True(release.IsOn);
			Assert.False(entries.Single(entry => entry.Id == "profile:Debug").IsOn);
		}

		[Fact]
		public void TheTemplatesAndTheRecentFilesAreEntries()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("recent.ssl", "procedure start begin\r\nend\r\n");

				IReadOnlyList<CommandEntry> entries = CommandCatalog.Create(harness.Main);

				Assert.Contains(entries, entry => entry.Id == "NewFromTemplateCommand:" + System.IO.Path.Combine(harness.Paths.TemplatesDirectory, "dialog.ssl"));
				Assert.Equal("File > New from Template > dialog", entries.Single(entry => entry.Title == "dialog").FullName);
				CommandEntry recent = entries.Single(entry => entry.Id == "OpenRecentCommand:" + document.FilePath);
				Assert.Equal("File > Open Recent > " + document.FilePath, recent.FullName);
			});
		}

		[Fact]
		public void TheRequirementOfEveryMenuItemIsAnEnumValueTheCatalogUnderstands()
		{
			foreach (CommandRequirement requirement in Enum.GetValues<CommandRequirement>())
			{
				Assert.Contains(requirement.ToString(), Enum.GetNames<CommandRequirement>());
			}

			foreach (CommandAvailability availability in Enum.GetValues<CommandAvailability>().Where(value => value != CommandAvailability.Available))
			{
				Assert.True(FalloutScriptStudio.Localization.Loc.TryGet("Str.Shell.Palette.Why." + availability, out String? text) && !String.IsNullOrWhiteSpace(text), availability.ToString());
			}
		}
	}
}