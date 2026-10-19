namespace FalloutScriptStudio.Tests.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Commands;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class CommandPaletteViewModelTests
	{
		private sealed class FakeGestures : ICommandGestureSource
		{
			public Dictionary<String, String> ById { get; } = new(StringComparer.Ordinal);

			public String? GetGesture(CommandEntry entry)
			{
				return this.ById.TryGetValue(entry.Id, out String? text) ? text : null;
			}
		}

		private static CommandEntry Entry(String id, String title, String menu = "Str.Shell.MenuBar.File", CommandAvailability availability = CommandAvailability.Available, Action? run = null)
		{
			return new CommandEntry(
				id,
				new[] { CommandText.FromKey(menu) },
				CommandText.FromLiteral(title),
				new RelayCommand(run ?? (() => { })),
				null,
				() => availability);
		}

		private static CommandPaletteViewModel Create(IReadOnlyList<CommandEntry> entries, IReadOnlyList<String>? recent = null, ICommandGestureSource? gestures = null)
		{
			CommandPaletteViewModel viewModel = new CommandPaletteViewModel();
			viewModel.Initialize(new CommandPaletteRequest
			{
				Entries = entries,
				RecentIds = recent ?? Array.Empty<String>(),
				Gestures = gestures,
			});
			return viewModel;
		}

		private static List<CommandEntry> Sample()
		{
			return
			[
				Entry("save", "Save"),
				Entry("saveall", "Save All"),
				Entry("saveas", "Save As"),
				Entry("format", "Format Document", "Str.Shell.MenuBar.Code"),
				Entry("zoom", "Zoom In", "Str.Shell.MenuBar.View"),
				Entry("compile", "Compile Script", "Str.Shell.MenuBar.Compile", CommandAvailability.NeedsScript),
			];
		}

		[Fact]
		public void WithoutAFilterEveryCommandIsListedInCatalogOrder()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			Assert.Equal(new[] { "save", "saveall", "saveas", "format", "zoom", "compile" }, viewModel.Items.Select(item => item.Entry.Id).ToArray());
			Assert.Equal("save", viewModel.SelectedItem!.Entry.Id);
			Assert.Equal("6 command(s)", viewModel.StatusText);
		}

		[Fact]
		public void TheRecentCommandsComeFirst_NewestFirst()
		{
			CommandPaletteViewModel viewModel = Create(Sample(), new[] { "zoom", "format", "saveas" });

			Assert.Equal(new[] { "saveas", "format", "zoom", "save", "saveall", "compile" }, viewModel.Items.Select(item => item.Entry.Id).ToArray());
			Assert.True(viewModel.Items[0].IsRecent);
			Assert.False(viewModel.Items[3].IsRecent);
			Assert.Equal("Recent", viewModel.Items[0].RecentText);
		}

		[Fact]
		public void TheFilterRanksAPrefixBeforeAWordStartBeforeLettersInOrder()
		{
			List<CommandEntry> entries =
			[
				Entry("a", "Close All Save"),
				Entry("b", "Safe Version"),
				Entry("c", "Save"),
				Entry("d", "Save All"),
			];
			CommandPaletteViewModel viewModel = Create(entries);

			viewModel.Filter = "sav";

			Assert.Equal(new[] { "c", "d", "a", "b" }, viewModel.Items.Select(item => item.Entry.Id).ToArray());
		}

		[Fact]
		public void TheFilterIgnoresTheCaseAndTheAccentsAndMatchesTheMenuPath()
		{
			List<CommandEntry> entries =
			[
				Entry("a", "Éditer"),
				Entry("b", "Format Document", "Str.Shell.MenuBar.Code"),
			];
			CommandPaletteViewModel viewModel = Create(entries);

			viewModel.Filter = "EDITER";
			Assert.Equal(new[] { "a" }, viewModel.Items.Select(item => item.Entry.Id).ToArray());

			viewModel.Filter = "code format";
			Assert.Equal(new[] { "b" }, viewModel.Items.Select(item => item.Entry.Id).ToArray());
		}

		[Fact]
		public void ARecentCommandWinsAmongEqualMatches()
		{
			CommandPaletteViewModel viewModel = Create(Sample(), new[] { "saveas" });

			viewModel.Filter = "sav";

			Assert.Equal("saveas", viewModel.Items[0].Entry.Id);
			viewModel.Filter = "save";
			Assert.Equal("save", viewModel.Items[0].Entry.Id);
		}

		[Fact]
		public void AFilterWithoutAMatchEmptiesTheListAndSaysSo()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			viewModel.Filter = "qqqq";

			Assert.Empty(viewModel.Items);
			Assert.Null(viewModel.SelectedItem);
			Assert.Equal("No command matches.", viewModel.StatusText);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void TheStatusCountFollowsTheFilter()
		{
			CommandPaletteViewModel viewModel = Create(Sample());
			List<String?> changed = [];
			viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

			viewModel.Filter = "save";

			Assert.Equal("3 command(s)", viewModel.StatusText);
			Assert.Contains(nameof(CommandPaletteViewModel.StatusText), changed);
		}

		[Fact]
		public void TheBestMatchIsSelectedAfterEachKeystroke()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			viewModel.Filter = "zo";

			Assert.Equal("zoom", viewModel.SelectedItem!.Entry.Id);
			viewModel.Filter = "sa";
			Assert.Equal("save", viewModel.SelectedItem!.Entry.Id);
		}

		[Fact]
		public void TheSelectionMovesWithinTheListAndStopsAtTheEnds()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			viewModel.MoveSelection(1);
			Assert.Equal("saveall", viewModel.SelectedItem!.Entry.Id);
			viewModel.MoveSelection(-5);
			Assert.Equal("save", viewModel.SelectedItem!.Entry.Id);
			viewModel.MoveSelection(100);
			Assert.Equal("compile", viewModel.SelectedItem!.Entry.Id);
		}

		[Fact]
		public void AcceptChoosesTheSelectedCommandAndClosesTheWindow()
		{
			CommandPaletteViewModel viewModel = Create(Sample());
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;
			viewModel.Filter = "zoom";

			viewModel.AcceptCommand.Execute(null);

			Assert.Equal("zoom", viewModel.ChosenEntry!.Id);
			Assert.True(closed);
		}

		[Fact]
		public void ADisabledCommandCannotBeChosen_AndTheReasonIsShown()
		{
			CommandPaletteViewModel viewModel = Create(Sample());
			Boolean closed = false;
			viewModel.CloseRequested += (_, _) => closed = true;
			viewModel.Filter = "compile";

			CommandPaletteItem item = viewModel.SelectedItem!;
			Assert.False(item.IsEnabled);
			Assert.Equal("Requires an open script", item.ReasonText);
			Assert.True(item.HasReason);
			Assert.Contains("Requires an open script", viewModel.StatusText, StringComparison.Ordinal);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
			viewModel.Accept();

			Assert.Null(viewModel.ChosenEntry);
			Assert.False(closed);
			Assert.Contains("unavailable: Requires an open script", item.AccessibleName, StringComparison.Ordinal);
		}

		[Fact]
		public void CancelClosesWithoutAChoice()
		{
			CommandPaletteViewModel viewModel = Create(Sample());
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CancelCommand.Execute(null);

			Assert.Null(viewModel.ChosenEntry);
			Assert.False(closed);
		}

		[Fact]
		public void TheShortcutComesFromTheSourceAndIsPartOfTheAccessibleName()
		{
			FakeGestures gestures = new FakeGestures();
			gestures.ById["save"] = "Ctrl+S";
			CommandPaletteViewModel viewModel = Create(Sample(), gestures: gestures);

			CommandPaletteItem item = viewModel.Items.Single(row => row.Entry.Id == "save");

			Assert.Equal("Ctrl+S", item.Gesture);
			Assert.Equal("Save, File, shortcut Ctrl+S", item.AccessibleName);
			Assert.Equal(String.Empty, viewModel.Items.Single(row => row.Entry.Id == "zoom").Gesture);
		}

		[Fact]
		public void AnEditorCommandWithoutAMenuShortcutUsesItsOwnGesture()
		{
			CommandEntry entry = new CommandEntry(
				"EditCommand:DeleteToLineEnd",
				new[] { CommandText.FromKey("Str.Shell.Palette.GroupEditor") },
				CommandText.FromKey("Str.Editor.Cmd.DeleteToLineEnd"),
				new RelayCommand(() => { }),
				EditorCommand.DeleteToLineEnd,
				() => CommandAvailability.Available);
			CommandPaletteViewModel viewModel = Create(new[] { entry }, gestures: new FakeGestures());

			Assert.Equal("Ctrl+Shift+D", viewModel.Items[0].Gesture);
		}

		[Fact]
		public void AStateIsShownForAnOptionAndForTheCurrentChoice()
		{
			Boolean on = true;
			CommandEntry toggle = new CommandEntry("t", Array.Empty<CommandText>(), CommandText.FromLiteral("Line Numbers"), new RelayCommand(() => { }), null, () => CommandAvailability.Available, () => on);
			CommandEntry choice = new CommandEntry("c", Array.Empty<CommandText>(), CommandText.FromLiteral("Dark"), new RelayCommand(() => { }), null, () => CommandAvailability.Available, () => true, true);
			CommandEntry other = new CommandEntry("o", Array.Empty<CommandText>(), CommandText.FromLiteral("Light"), new RelayCommand(() => { }), null, () => CommandAvailability.Available, () => false, true);

			CommandPaletteViewModel viewModel = Create(new[] { toggle, choice, other });

			Assert.Equal("On", viewModel.Items[0].StateText);
			Assert.Equal("Current", viewModel.Items[1].StateText);
			Assert.Equal(String.Empty, viewModel.Items[2].StateText);
			Assert.Equal("Line Numbers, On", viewModel.Items[0].AccessibleName);
			Assert.Equal(String.Empty, viewModel.Items[0].PathText);
		}

		[Fact]
		public void ALanguageChangeRebuildsTheRowsAndAnnouncesTheNewTexts()
		{
			LanguageCollection.CollectGarbage();
			CommandEntry entry = new CommandEntry(
				"SaveCommand",
				new[] { CommandText.FromKey("Str.Shell.MenuBar.File") },
				CommandText.FromKey("Str.Common.Save"),
				new RelayCommand(() => { }),
				null,
				() => CommandAvailability.Available);
			CommandPaletteViewModel viewModel = Create(new[] { entry });
			List<String?> changed = [];
			viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
			CommandPaletteItem before = viewModel.Items[0];
			try
			{
				Assert.Equal("File > Save", before.FullName);

				Loc.SetLanguage("de");

				Assert.NotSame(before, viewModel.Items[0]);
				Assert.Contains(nameof(CommandPaletteViewModel.Title), changed);
				Assert.Contains(nameof(CommandPaletteViewModel.StatusText), changed);
			}
			finally
			{
				Loc.SetLanguage("en");
				GC.KeepAlive(viewModel);
			}
		}

		[Fact]
		public void WithoutAFilterTheNameIsOneUnemphasizedPiece()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			PaletteTextPart part = Assert.Single(viewModel.Items[1].NameParts);
			Assert.Equal("Save All", part.Text);
			Assert.False(part.IsMatch);
		}

		[Fact]
		public void TheMatchedLettersOfTheFilterAreSeparatedFromTheRestOfTheName()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			viewModel.Filter = "sav";

			CommandPaletteItem saveAll = viewModel.Items.Single(item => item.Entry.Id == "saveall");
			Assert.Equal(new[] { "Sav|True", "e All|False" }, saveAll.NameParts.Select(part => part.Text + "|" + part.IsMatch).ToArray());
			Assert.Equal("Save All", String.Concat(saveAll.NameParts.Select(part => part.Text)));
		}

		[Fact]
		public void SeveralWordsAndLettersInOrderAreEmphasizedAndTheAccentsAreIgnored()
		{
			CommandPaletteViewModel viewModel = Create(new[] { Entry("a", "Éditer le Fichier") });

			viewModel.Filter = "edit fich";

			Assert.Equal(new[] { "Édit|True", "er le |False", "Fich|True", "ier|False" }, viewModel.Items[0].NameParts.Select(part => part.Text + "|" + part.IsMatch).ToArray());
		}

		[Fact]
		public void ClearingTheFilterRemovesTheEmphasis()
		{
			CommandPaletteViewModel viewModel = Create(Sample());
			viewModel.Filter = "zoom";

			viewModel.Filter = String.Empty;

			CommandPaletteItem zoom = viewModel.Items.Single(item => item.Entry.Id == "zoom");
			Assert.Equal("Zoom In", Assert.Single(zoom.NameParts).Text);
			Assert.All(zoom.NameParts, part => Assert.False(part.IsMatch));
		}

		[Fact]
		public void TheTrailingTextPutsTheStateTheRecentMarkAndTheShortcutOnOneLine()
		{
			FakeGestures gestures = new FakeGestures();
			gestures.ById["t"] = "Ctrl+L";
			CommandEntry toggle = new CommandEntry("t", Array.Empty<CommandText>(), CommandText.FromLiteral("Line Numbers"), new RelayCommand(() => { }), null, () => CommandAvailability.Available, () => true);
			CommandPaletteViewModel viewModel = Create(new[] { toggle, Entry("save", "Save"), Entry("zoom", "Zoom In") }, new[] { "t" }, gestures);

			Assert.Equal("On  Recent  Ctrl+L", viewModel.Items.Single(item => item.Entry.Id == "t").TrailingText);
			Assert.Equal(String.Empty, viewModel.Items.Single(item => item.Entry.Id == "zoom").TrailingText);
		}

		[Fact]
		public void ADisabledCommandKeepsItsReasonOutOfTheVisibleTextOfTheRow()
		{
			CommandPaletteViewModel viewModel = Create(Sample());

			CommandPaletteItem compile = viewModel.Items.Single(item => item.Entry.Id == "compile");

			Assert.True(compile.HasReason);
			Assert.DoesNotContain("Requires", compile.TrailingText, StringComparison.Ordinal);
			Assert.DoesNotContain("Requires", compile.PathText, StringComparison.Ordinal);
		}

		[Fact]
		public void TheFilterIsFastOverAThousandCommands()
		{
			List<CommandEntry> many = Enumerable.Range(0, 1000).Select(i => Entry("id" + i, "Command number " + i, "Str.Shell.MenuBar.Code")).ToList();
			CommandPaletteViewModel viewModel = Create(many);
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

			viewModel.Filter = "cmd 77";

			Assert.True(clock.ElapsedMilliseconds < 500, "Filtering took " + clock.ElapsedMilliseconds + " ms.");
			Assert.NotEmpty(viewModel.Items);
		}
	}
}