namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using Xunit;

	public sealed class MacroRenamePreviewViewModelTests
	{
		private const String OpenFile = "C:\\scripts\\one.ssl";
		private const String ProjectFile = "C:\\project\\two.h";

		private static PreviewMatch Match(String file, Boolean open, Int32 line, String text)
		{
			return new PreviewMatch(file, open, new TextSpan(line * 10, 3), line, text);
		}

		private static MacroRenamePreview CreatePreview(params PreviewMatch[] matches)
		{
			ScriptMacro macro = new ScriptMacro("FOO", "FOO(a, b)", "(a + b)", "C:\\project\\two.h", 4, null);
			return new MacroRenamePreview(macro, "BAR", Array.Empty<RefactorDocument>(), matches);
		}

		private static MacroRenamePreviewViewModel Create(params PreviewMatch[] matches)
		{
			MacroRenamePreviewViewModel viewModel = new MacroRenamePreviewViewModel();
			viewModel.Initialize(CreatePreview(matches));
			return viewModel;
		}

		private static PreviewMatch[] SampleMatches()
		{
			return new[]
			{
				Match(OpenFile, true, 1, "FOO(1, 2);"),
				Match(OpenFile, true, 7, "FOO(3, 4) + 1"),
				Match(ProjectFile, false, 4, "FOO(a, b) (a + b)"),
				Match(ProjectFile, false, 9, "FOO"),
			};
		}

		[Fact]
		public void Initialize_ShowsTheDefinitionBeforeAndAfter()
		{
			MacroRenamePreviewViewModel viewModel = Create(SampleMatches());

			Assert.Equal("#define FOO(a, b) (a + b)", viewModel.OldDefinition);
			Assert.Equal("#define BAR(a, b) (a + b)", viewModel.NewDefinition);
			Assert.Contains("two.h", viewModel.DeclaredAt);
			Assert.Contains("line 4", viewModel.DeclaredAt);
		}

		[Fact]
		public void RenameDefinition_ReplacesOnlyTheLeadingName()
		{
			Assert.Equal("BAR(FOO, b)", MacroRenamePreviewViewModel.RenameDefinition("FOO(FOO, b)", "FOO", "BAR"));
			Assert.Equal("BAR", MacroRenamePreviewViewModel.RenameDefinition("FOO", "FOO", "BAR"));
		}

		[Fact]
		public void Initialize_GroupsMatchesByOpenScriptsAndProjectFiles()
		{
			MacroRenamePreviewViewModel viewModel = Create(SampleMatches());

			Assert.Equal(2, viewModel.Sections.Count);
			MacroRenameNodeViewModel open = viewModel.Sections[0];
			MacroRenameNodeViewModel project = viewModel.Sections[1];
			Assert.Equal("Open scripts", open.Header);
			Assert.Equal("Files in the project folder", project.Header);
			MacroRenameNodeViewModel file = Assert.Single(open.Children);
			Assert.Equal("one.ssl", file.Header);
			Assert.Equal(2, file.Children.Count);
			Assert.Equal("Line 1", file.Children[0].Header);
			Assert.Equal("FOO", file.Children[0].MatchText);
			Assert.Equal("(1, 2);", file.Children[0].RestText);
			Assert.Equal("2 matches", file.CountText);
			Assert.Equal(4, viewModel.TotalCount);
			Assert.Equal(4, viewModel.SelectedCount);
			Assert.Contains("4 of 4", viewModel.Summary);
		}

		[Fact]
		public void Initialize_ProjectFilesOnly_HasOneSection()
		{
			MacroRenamePreviewViewModel viewModel = Create(Match(ProjectFile, false, 2, "FOO"));

			MacroRenameNodeViewModel section = Assert.Single(viewModel.Sections);
			Assert.Equal("Files in the project folder", section.Header);
		}

		[Fact]
		public void Initialize_WithoutMatches_IsEmptyAndCannotApply()
		{
			MacroRenamePreviewViewModel viewModel = Create();

			Assert.True(viewModel.IsEmpty);
			Assert.Empty(viewModel.Sections);
			Assert.False(viewModel.ApplyCommand.CanExecute(null));
		}

		[Fact]
		public void Apply_ReturnsAllMatchesInPreviewOrder_WhenNothingWasUnticked()
		{
			PreviewMatch[] matches = SampleMatches();
			MacroRenamePreviewViewModel viewModel = Create(matches);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.ApplyCommand.Execute(null);

			Assert.True(closed);
			Assert.Equal(matches, viewModel.AcceptedMatches);
		}

		[Fact]
		public void UntickingAMatch_RemovesOnlyThatMatchAndMakesTheParentsUndetermined()
		{
			PreviewMatch[] matches = SampleMatches();
			MacroRenamePreviewViewModel viewModel = Create(matches);
			MacroRenameNodeViewModel file = viewModel.Sections[0].Children[0];

			file.Children[1].IsChecked = false;

			Assert.Null(file.IsChecked);
			Assert.Null(viewModel.Sections[0].IsChecked);
			Assert.True(viewModel.Sections[1].IsChecked);
			Assert.Equal(3, viewModel.SelectedCount);

			viewModel.ApplyCommand.Execute(null);

			Assert.Equal(new[] { matches[0], matches[2], matches[3] }, viewModel.AcceptedMatches);
		}

		[Fact]
		public void UntickingAFile_UntickAllItsMatches()
		{
			PreviewMatch[] matches = SampleMatches();
			MacroRenamePreviewViewModel viewModel = Create(matches);

			viewModel.Sections[1].Children[0].IsChecked = false;

			Assert.All(viewModel.Sections[1].Children[0].Children, child => Assert.False(child.IsChecked));
			Assert.False(viewModel.Sections[1].IsChecked);
			Assert.True(viewModel.Sections[0].IsChecked);
			Assert.Equal(2, viewModel.SelectedCount);
			viewModel.ApplyCommand.Execute(null);
			Assert.Equal(new[] { matches[0], matches[1] }, viewModel.AcceptedMatches);
		}

		[Fact]
		public void RetickingTheLastMatch_RestoresTheParents()
		{
			MacroRenamePreviewViewModel viewModel = Create(SampleMatches());
			MacroRenameNodeViewModel file = viewModel.Sections[0].Children[0];
			file.Children[0].IsChecked = false;
			file.Children[1].IsChecked = false;

			Assert.False(file.IsChecked);
			Assert.False(viewModel.Sections[0].IsChecked);

			file.Children[1].IsChecked = true;
			file.Children[0].IsChecked = true;

			Assert.True(file.IsChecked);
			Assert.True(viewModel.Sections[0].IsChecked);
			Assert.Equal(4, viewModel.SelectedCount);
		}

		[Fact]
		public void DeselectAll_ThenSelectAll_TogglesEverythingAndTheApplyButton()
		{
			MacroRenamePreviewViewModel viewModel = Create(SampleMatches());

			viewModel.DeselectAllCommand.Execute(null);

			Assert.Equal(0, viewModel.SelectedCount);
			Assert.False(viewModel.ApplyCommand.CanExecute(null));
			Assert.All(viewModel.Sections.SelectMany(section => section.EnumerateMatches()), node => Assert.False(node.IsChecked));
			Assert.Contains("0 of 4", viewModel.Summary);

			viewModel.SelectAllCommand.Execute(null);

			Assert.Equal(4, viewModel.SelectedCount);
			Assert.True(viewModel.ApplyCommand.CanExecute(null));
			Assert.All(viewModel.Sections.SelectMany(section => section.EnumerateMatches()), node => Assert.True(node.IsChecked));
		}

		[Fact]
		public void Cancel_ReturnsNoMatches()
		{
			MacroRenamePreviewViewModel viewModel = Create(SampleMatches());
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CancelCommand.Execute(null);

			Assert.False(closed);
			Assert.Null(viewModel.AcceptedMatches);
		}

		[Fact]
		public void Initialize_AgainReplacesTheTree()
		{
			MacroRenamePreviewViewModel viewModel = Create(SampleMatches());
			viewModel.Sections[0].IsChecked = false;

			viewModel.Initialize(CreatePreview(Match(ProjectFile, false, 2, "FOO")));

			Assert.Equal(1, viewModel.TotalCount);
			Assert.Equal(1, viewModel.SelectedCount);
			List<MacroRenameNodeViewModel> nodes = viewModel.Sections.SelectMany(section => section.EnumerateMatches()).ToList();
			Assert.Single(nodes);
		}

		[Fact]
		public void MatchLongerThanThePreviewText_IsClamped()
		{
			PreviewMatch match = new PreviewMatch(ProjectFile, false, new TextSpan(0, 50), 1, "FOO");
			MacroRenamePreviewViewModel viewModel = Create(match);

			MacroRenameNodeViewModel node = viewModel.Sections[0].Children[0].Children[0];
			Assert.Equal("FOO", node.MatchText);
			Assert.Equal(String.Empty, node.RestText);
		}
	}
}