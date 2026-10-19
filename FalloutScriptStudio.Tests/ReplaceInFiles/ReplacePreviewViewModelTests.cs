namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class ReplacePreviewViewModelTests
	{
		private static ReplaceMatch Match(Int32 id, Int32 line, String matched = "old", String replacement = "new")
		{
			return new ReplaceMatch(id, line, 5, id * 10, matched.Length, matched, replacement, "x = ", ";");
		}

		private static ReplaceFilePlan File(String path, Int32 matches)
		{
			return new ReplaceFilePlan(path, new Byte[32], Enumerable.Range(0, matches).Select(i => Match(i, i + 1)).ToList(), null);
		}

		private static ReplacePlan Plan(IReadOnlyList<ReplaceFilePlan> files, IReadOnlyList<ReplaceSkip>? skipped = null, Boolean truncated = false)
		{
			return new ReplacePlan(files, skipped ?? Array.Empty<ReplaceSkip>(), files.Count, truncated, Encoding.UTF8);
		}

		private static ReplaceInFilesRequest Request(String find = "old", String replacement = "new", Boolean regex = false)
		{
			return new ReplaceInFilesRequest { Matcher = ReplaceInFilesPlannerTests.Matcher(find, true, false, regex), Replacement = replacement };
		}

		private static ReplacePreviewViewModel Create(params ReplaceFilePlan[] files)
		{
			ReplacePreviewViewModel viewModel = new ReplacePreviewViewModel();
			viewModel.Initialize(Plan(files), Request());
			return viewModel;
		}

		[Fact]
		public void EveryReplacementIsTickedAtFirst_AndTheSummaryCountsThem()
		{
			ReplacePreviewViewModel viewModel = Create(File(@"C:\m\a.ssl", 30), File(@"C:\m\b.ssl", 12));

			Assert.Equal(2, viewModel.Files.Count);
			Assert.Equal((42, 42, 2), (viewModel.TotalCount, viewModel.SelectedCount, viewModel.SelectedFileCount));
			Assert.Equal("42 replacements in 2 files (of 42 found)", viewModel.Summary);
			Assert.All(viewModel.Files, file => Assert.True(file.IsChecked));
			Assert.True(viewModel.ApplyCommand.CanExecute(null));
		}

		[Fact]
		public void TheSummaryUsesTheRightWordsForOneAndForNothing()
		{
			ReplacePreviewViewModel one = Create(File(@"C:\m\a.ssl", 1));
			ReplacePreviewViewModel several = Create(File(@"C:\m\a.ssl", 5));
			several.DeselectAllCommand.Execute(null);

			Assert.Equal("1 replacement in 1 file (of 1 found)", one.Summary);
			Assert.Equal("Nothing selected (5 replacements found)", several.Summary);
			Assert.False(several.ApplyCommand.CanExecute(null));
		}

		[Fact]
		public void TickingAFileTicksItsReplacements_AndUntickingOneMakesTheFileUndetermined()
		{
			ReplacePreviewViewModel viewModel = Create(File(@"C:\m\a.ssl", 3), File(@"C:\m\b.ssl", 2));
			ReplaceNodeViewModel a = viewModel.Files[0];

			a.Children[1].IsChecked = false;
			Assert.Null(a.IsChecked);
			Assert.Equal(4, viewModel.SelectedCount);
			Assert.Equal("4 replacements in 2 files (of 5 found)", viewModel.Summary);

			a.IsChecked = false;
			Assert.All(a.Children, child => Assert.False(child.IsChecked));
			Assert.Equal(2, viewModel.SelectedCount);
			Assert.Equal("2 replacements in 1 file (of 5 found)", viewModel.Summary);

			a.IsChecked = true;
			Assert.All(a.Children, child => Assert.True(child.IsChecked));
			Assert.Equal(5, viewModel.SelectedCount);
		}

		[Fact]
		public void UntickingEveryReplacementOfAFileUntiksTheFile_TickingThemAllTicksItAgain()
		{
			ReplacePreviewViewModel viewModel = Create(File(@"C:\m\a.ssl", 2));
			ReplaceNodeViewModel file = viewModel.Files[0];

			file.Children[0].IsChecked = false;
			file.Children[1].IsChecked = false;
			Assert.False(file.IsChecked);
			file.Children[0].IsChecked = true;
			Assert.Null(file.IsChecked);
			file.Children[1].IsChecked = true;
			Assert.True(file.IsChecked);
		}

		[Fact]
		public void ApplyReturnsOnlyTheTickedReplacementsOfEachFile_InFileOrder()
		{
			ReplacePreviewViewModel viewModel = Create(File(@"C:\m\a.ssl", 3), File(@"C:\m\b.ssl", 2), File(@"C:\m\c.ssl", 1));
			viewModel.Files[0].Children[1].IsChecked = false;
			viewModel.Files[1].IsChecked = false;
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.ApplyCommand.Execute(null);

			Assert.True(closed);
			Assert.Equal(2, viewModel.Selection!.Count);
			Assert.Equal(new[] { 0, 2 }, viewModel.Selection[0].Matches.Select(match => match.Id).ToArray());
			Assert.Equal(@"C:\m\c.ssl", viewModel.Selection[1].File.Path);
		}

		[Fact]
		public void CancelReturnsNoSelection()
		{
			ReplacePreviewViewModel viewModel = Create(File(@"C:\m\a.ssl", 3));
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CancelCommand.Execute(null);

			Assert.False(closed);
			Assert.Null(viewModel.Selection);
		}

		[Fact]
		public void SelectAllAndDeselectAllAreBulkOperations_ThatStayFast()
		{
			ReplacePreviewViewModel viewModel = Create(Enumerable.Range(0, 100).Select(i => File(@"C:\m\f" + i + ".ssl", 500)).ToArray());
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

			viewModel.DeselectAllCommand.Execute(null);
			Assert.Equal(0, viewModel.SelectedCount);
			viewModel.SelectAllCommand.Execute(null);

			Assert.Equal(50000, viewModel.SelectedCount);
			Assert.Equal(100, viewModel.SelectedFileCount);
			Assert.True(clock.ElapsedMilliseconds < 3000, "Ticking took " + clock.ElapsedMilliseconds + " ms.");
		}

		[Fact]
		public void TheNodesCarryTheTextsTheTemplateShows()
		{
			ReplacePreviewViewModel viewModel = new ReplacePreviewViewModel();
			viewModel.Initialize(Plan(new[] { File(@"C:\m\sub\a.ssl", 2) }), Request());
			ReplaceNodeViewModel file = viewModel.Files[0];
			ReplaceNodeViewModel match = file.Children[1];

			Assert.Equal("a.ssl", file.Header);
			Assert.Equal(@"C:\m\sub", file.Detail);
			Assert.Equal("2 replacements", file.CountText);
			Assert.Equal("a.ssl, 2 replacements", file.AccessibleName);
			Assert.Equal("Line 2", match.Header);
			Assert.Equal(("x = ", "old", "new", ";"), (match.Prefix, match.Matched, match.Replacement, match.Suffix));
			Assert.Equal("Line 2: x = old; becomes x = new;", match.AccessibleName);
			Assert.True(file.IsFile);
			Assert.False(match.IsFile);
			Assert.True(file.IsExpanded);
		}

		[Fact]
		public void ADocumentThatWasNeverSavedGetsAReadableName()
		{
			ReplaceFilePlan file = new ReplaceFilePlan(FalloutScriptStudio.Core.Compilation.BuildError.NoFile, new Byte[32], new[] { Match(0, 1) }, new Object());
			ReplacePreviewViewModel viewModel = new ReplacePreviewViewModel();
			viewModel.Initialize(Plan(new[] { file }), Request());

			Assert.Equal("(unsaved script)", viewModel.Files[0].Header);
			Assert.Equal(String.Empty, viewModel.Files[0].Detail);
		}

		[Fact]
		public void TheRequestIsShownAboveTheTree()
		{
			ReplacePreviewViewModel viewModel = new ReplacePreviewViewModel();
			ReplaceInFilesRequest request = new ReplaceInFilesRequest { Matcher = ReplaceInFilesPlannerTests.Matcher(@"set\((\w+)\)", true, false, true), Replacement = "put($1)" };

			viewModel.Initialize(Plan(new[] { File(@"C:\m\a.ssl", 1) }), request);

			Assert.Equal((@"set\((\w+)\)", "put($1)"), (viewModel.FindText, viewModel.ReplaceText));
			Assert.Equal("Match case, Regular expression", viewModel.OptionsText);
			Assert.Equal("Replace in Files", viewModel.Title);
		}

		[Fact]
		public void ATruncatedPlanAndTheLeftOutFilesAreSaid()
		{
			ReplacePreviewViewModel viewModel = new ReplacePreviewViewModel();
			ReplaceSkip[] skipped =
			{
				new ReplaceSkip(@"C:\m\ro.ssl", ReplaceSkipReason.ReadOnly, 4),
				new ReplaceSkip(@"C:\m\locked.ssl", ReplaceSkipReason.Unreadable, 0),
			};
			viewModel.Initialize(Plan(new[] { File(@"C:\m\a.ssl", 3) }, skipped, true), Request());

			Assert.True(viewModel.IsTruncated);
			Assert.Equal("Only the first 3 replacements are listed. Run Replace in Files again afterwards to find the rest.", viewModel.TruncatedText);
			Assert.True(viewModel.HasSkipped);
			Assert.Equal("2 file(s) left out", viewModel.SkippedText);
			Assert.Equal(@"C:\m\ro.ssl is read-only (4 replacements not offered)", viewModel.SkippedItems[0]);
			Assert.Equal(@"C:\m\locked.ssl cannot be read (it may be locked)", viewModel.SkippedItems[1]);
		}

		[Fact]
		public void ALanguageChangeRefreshesTheTexts()
		{
			LanguageCollection.CollectGarbage();
			ReplacePreviewViewModel viewModel = Create(File(@"C:\m\a.ssl", 2));
			List<String?> changed = [];
			viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
			try
			{
				FalloutScriptStudio.Localization.Loc.SetLanguage("de");

				Assert.Contains(nameof(ReplacePreviewViewModel.Summary), changed);
				Assert.Contains(nameof(ReplacePreviewViewModel.Title), changed);
			}
			finally
			{
				FalloutScriptStudio.Localization.Loc.SetLanguage("en");
				GC.KeepAlive(viewModel);
			}
		}
	}
}