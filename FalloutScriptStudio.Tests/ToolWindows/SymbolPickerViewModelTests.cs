namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class SymbolPickerViewModelTests
	{
		private const String Root = @"C:\mod";

		private static FakeSymbolIndex CreateIndex()
		{
			FakeSymbolIndex index = new FakeSymbolIndex();
			index.Symbols.Add(new IndexedSymbol("get_level", IndexedSymbolKind.Procedure, Root + @"\scripts\a.ssl", 10));
			index.Symbols.Add(new IndexedSymbol("level_up", IndexedSymbolKind.Procedure, Root + @"\scripts\b.ssl", 20));
			index.Symbols.Add(new IndexedSymbol("LEVEL_MAX", IndexedSymbolKind.Macro, Root + @"\headers\c.h", 3));
			index.Symbols.Add(new IndexedSymbol("MAX", IndexedSymbolKind.Macro, Root + @"\headers\c.h", 4) { Detail = "MAX(a, b)" });
			index.Symbols.Add(new IndexedSymbol("current_level", IndexedSymbolKind.Variable, Root + @"\scripts\a.ssl", 2) { Scope = VariableScope.Export });
			index.Symbols.Add(new IndexedSymbol("api", IndexedSymbolKind.Procedure, Root + @"\scripts\a.ssl", 5) { HasBody = false });
			return index;
		}

		private static SymbolPickerViewModel CreateSearch(FakeSymbolIndex index)
		{
			SymbolPickerViewModel viewModel = new SymbolPickerViewModel();
			viewModel.Initialize(new SymbolPickerRequest { Mode = SymbolPickerMode.Search, Index = index, RootFolder = Root });
			return viewModel;
		}

		[Fact]
		public void TheSearchListStartsWithTheSymbolsByName_AndSelectsTheFirst()
		{
			FakeSymbolIndex index = CreateIndex();
			SymbolPickerViewModel viewModel = CreateSearch(index);

			Assert.True(viewModel.IsFilterVisible);
			Assert.False(viewModel.IsPromptVisible);
			Assert.Equal("Go to Symbol", viewModel.Title);
			Assert.Equal(6, viewModel.Items.Count);
			Assert.Same(viewModel.Items[0], viewModel.SelectedItem);
			Assert.Equal((String.Empty, SymbolPickerRequest.DefaultMaxResults), (index.Searches[^1].Filter ?? String.Empty, index.Searches[^1].Max));
			Assert.Equal("6 of 6 symbols", viewModel.StatusText);
		}

		[Fact]
		public void TypingFiltersTheList_RanksTheMatches_AndSelectsTheBestOne()
		{
			SymbolPickerViewModel viewModel = CreateSearch(CreateIndex());

			viewModel.Filter = "level";

			Assert.Equal(new[] { "level_up", "LEVEL_MAX", "get_level", "current_level" }, viewModel.Items.Select(item => item.DisplayName).ToArray());
			Assert.Equal("level_up", viewModel.SelectedItem?.DisplayName);
			Assert.Equal("4 of 6 symbols", viewModel.StatusText);

			viewModel.Filter = "zzzz";
			Assert.Empty(viewModel.Items);
			Assert.Null(viewModel.SelectedItem);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void TheArrowKeysMoveTheSelectionWithinTheList()
		{
			SymbolPickerViewModel viewModel = CreateSearch(CreateIndex());
			viewModel.Filter = "level";

			viewModel.MoveSelection(1);
			Assert.Equal("LEVEL_MAX", viewModel.SelectedItem?.DisplayName);
			viewModel.MoveSelection(10);
			Assert.Equal("current_level", viewModel.SelectedItem?.DisplayName);
			viewModel.MoveSelection(-10);
			Assert.Equal("level_up", viewModel.SelectedItem?.DisplayName);
		}

		[Fact]
		public void AcceptChoosesTheSelectedSymbolAndClosesTheWindow()
		{
			SymbolPickerViewModel viewModel = CreateSearch(CreateIndex());
			viewModel.Filter = "max";
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			Assert.True(viewModel.AcceptCommand.CanExecute(null));
			viewModel.AcceptCommand.Execute(null);

			Assert.True(closed);
			Assert.Equal("MAX", viewModel.SelectedSymbol?.Name);
		}

		[Fact]
		public void CancelChoosesNothing()
		{
			SymbolPickerViewModel viewModel = CreateSearch(CreateIndex());
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CancelCommand.Execute(null);

			Assert.False(closed);
			Assert.Null(viewModel.SelectedSymbol);
		}

		[Fact]
		public void TheItemsTellTheKindTheFileRelativeToTheProjectAndTheLine()
		{
			SymbolPickerViewModel viewModel = CreateSearch(CreateIndex());
			viewModel.Filter = "max";

			SymbolListItem macro = viewModel.Items.Single(item => item.Symbol.Name == "MAX");
			Assert.Equal("MAX(a, b)", macro.DisplayName);
			Assert.Equal("Macro", macro.KindText);
			Assert.Equal(@"headers\c.h", macro.FileText);
			Assert.Equal(@"headers\c.h (line 4)", macro.LocationText);
			Assert.Equal(@"Macro MAX(a, b), headers\c.h, line 4", macro.AccessibleName);
			Assert.Equal(Wpf.Ui.Controls.SymbolRegular.Tag24, macro.Icon);

			viewModel.Filter = "api";
			Assert.Equal("Procedure (declaration)", viewModel.Items.Single().KindText);
			viewModel.Filter = "current";
			SymbolListItem variable = viewModel.Items.Single();
			Assert.Equal("Exported variable", variable.KindText);
			Assert.Equal(Wpf.Ui.Controls.SymbolRegular.BracesVariable24, variable.Icon);
			viewModel.Filter = "get_level";
			Assert.Equal(Wpf.Ui.Controls.SymbolRegular.Code24, viewModel.Items.Single().Icon);
		}

		[Fact]
		public void AFileOutsideTheRootIsShownWithItsFullPath()
		{
			SymbolListItem item = new SymbolListItem(new IndexedSymbol("x", IndexedSymbolKind.Procedure, @"D:\other\x.ssl", 1), Root);

			Assert.Equal(@"D:\other\x.ssl", item.FileText);
			Assert.Equal("x.ssl", new SymbolListItem(new IndexedSymbol("x", IndexedSymbolKind.Procedure, @"D:\other\x.ssl", 1), null).Symbol.FileName);
		}

		[Fact]
		public void TheStatusFollowsTheProgressOfTheIndexing_AndTheListRefreshesWhenTheIndexIsUpdated()
		{
			ShellStaRunner.Run(async () =>
			{
				FakeSymbolIndex index = new FakeSymbolIndex();
				SymbolPickerViewModel viewModel = CreateSearch(index);
				Assert.Equal("No symbol has been indexed yet.", viewModel.StatusText);

				index.SetProgress(new ProjectIndexProgress(ProjectIndexPhase.Scanning, 0, 0, 0));
				Assert.Equal("Listing the files of the project...", viewModel.StatusText);
				index.SetProgress(new ProjectIndexProgress(ProjectIndexPhase.Indexing, 1200, 5000, 0));
				Assert.Equal("Indexing the project: 1200 of 5000 files", viewModel.StatusText);

				index.Symbols.Add(new IndexedSymbol("late", IndexedSymbolKind.Procedure, Root + @"\a.ssl", 1));
				index.SetProgress(ProjectIndexProgress.Idle);
				index.RaiseUpdated();

				Assert.True(await Eventually.TrueAsync(() => viewModel.Items.Count == 1));
				Assert.Equal(new[] { "late" }, viewModel.Items.Select(item => item.DisplayName).ToArray());
				Assert.Equal("1 of 1 symbols", viewModel.StatusText);
			});
		}

		[Fact]
		public void AnUpdateOfTheIndexKeepsTheSelectedSymbol()
		{
			ShellStaRunner.Run(async () =>
			{
				FakeSymbolIndex index = CreateIndex();
				SymbolPickerViewModel viewModel = CreateSearch(index);
				viewModel.MoveSelection(2);
				IndexedSymbol selected = viewModel.SelectedItem!.Symbol;

				index.Symbols.Add(new IndexedSymbol("zzz", IndexedSymbolKind.Macro, Root + @"\z.h", 1));
				index.RaiseUpdated();

				Assert.True(await Eventually.TrueAsync(() => viewModel.Items.Count == 7));
				Assert.Same(selected, viewModel.SelectedItem?.Symbol);
			});
		}

		[Fact]
		public void ADisposedListNoLongerFollowsTheIndex()
		{
			ShellStaRunner.Run(async () =>
			{
				FakeSymbolIndex index = CreateIndex();
				SymbolPickerViewModel viewModel = CreateSearch(index);
				viewModel.Dispose();
				Int32 searches = index.Searches.Count;

				index.RaiseUpdated();
				await Task.Delay(100);

				Assert.Equal(searches, index.Searches.Count);
			});
		}

		[Fact]
		public void TheChoiceListHasNoFilterAndKeepsTheOrderOfTheCandidates()
		{
			SymbolPickerViewModel viewModel = new SymbolPickerViewModel();
			IndexedSymbol[] candidates =
			{
				new IndexedSymbol("remote_proc", IndexedSymbolKind.Procedure, Root + @"\scripts\b.ssl", 6),
				new IndexedSymbol("remote_proc", IndexedSymbolKind.Procedure, Root + @"\other\c.ssl", 2),
			};

			viewModel.Initialize(new SymbolPickerRequest { Mode = SymbolPickerMode.Choose, Candidates = candidates, Name = "remote_proc", RootFolder = Root });

			Assert.False(viewModel.IsFilterVisible);
			Assert.True(viewModel.IsPromptVisible);
			Assert.Equal("Choose a Location", viewModel.Title);
			Assert.Equal("remote_proc is defined in several places. Choose one.", viewModel.Prompt);
			Assert.Equal(new[] { @"scripts\b.ssl", @"other\c.ssl" }, viewModel.Items.Select(item => item.FileText).ToArray());
			Assert.Equal("2 location(s)", viewModel.StatusText);
			viewModel.MoveSelection(1);
			viewModel.AcceptCommand.Execute(null);
			Assert.Same(candidates[1], viewModel.SelectedSymbol);
		}

		[Fact]
		public void TheTextsFollowALanguageChange()
		{
			LanguageCollection.CollectGarbage();
			SymbolPickerViewModel viewModel = CreateSearch(CreateIndex());
			viewModel.Filter = "max";
			List<String?> changed = new List<String?>();
			viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
			try
			{
				Loc.SetLanguage("fr");

				Assert.Contains(nameof(SymbolPickerViewModel.Title), changed);
				Assert.Contains(nameof(SymbolPickerViewModel.Prompt), changed);
				Assert.Contains(nameof(SymbolPickerViewModel.StatusText), changed);
			}
			finally
			{
				Loc.SetLanguage(Loc.DefaultLanguage);
			}
		}

		[Fact]
		public void EveryNewTextExistsInEnglish()
		{
			String[] keys =
			{
				"Str.ToolWindows.Symbols.Title", "Str.ToolWindows.Symbols.Search", "Str.ToolWindows.Symbols.Open", "Str.ToolWindows.Symbols.ChooseTitle",
				"Str.ToolWindows.Symbols.ChoosePrompt", "Str.ToolWindows.Symbols.Locations", "Str.ToolWindows.Symbols.Count", "Str.ToolWindows.Symbols.Indexing",
				"Str.ToolWindows.Symbols.Scanning", "Str.ToolWindows.Symbols.NoneIndexed", "Str.ToolWindows.Symbols.KindProcedure", "Str.ToolWindows.Symbols.KindProcedureDeclaration",
				"Str.ToolWindows.Symbols.KindMacro", "Str.ToolWindows.Symbols.KindVariable", "Str.ToolWindows.Symbols.KindImportedVariable", "Str.ToolWindows.Symbols.KindExportedVariable",
				"Str.ToolWindows.Symbols.Location", "Str.ToolWindows.Symbols.ItemName",
			};

			foreach (String key in keys)
			{
				Assert.True(Loc.TryGet(key, out String? text), key);
				Assert.NotEqual(key, text);
			}
		}
	}
}
