namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class CompareMessagesViewModelTests
	{
		private const String English = @"C:\game\text\english\dialog\hakunin.msg";
		private const String French = @"C:\game\text\french\dialog\hakunin.msg";
		private const String German = @"C:\game\text\german\dialog\hakunin.msg";

		private static readonly String[] _english =
		{
			"# NPC",
			"{100}{}{Hello there.}",
			"\t{101}{}{Goodbye.}",
			"{102}{}{You have %d caps.}",
			"{103}{}{Thanks.}",
			"{104}{}{Welcome.}",
		};

		private static readonly String[] _french =
		{
			"{100}{}{Bonjour.}",
			"{101}{}{Au revoir.}",
			"{102}{}{Vous avez %d et %s caps.}",
			"{103}{}{Thanks.}",
			"{104}{}{ }",
			"{999}{}{En trop.}",
		};

		private sealed class Dialogs : IDialogService
		{
			public Boolean ConfirmAnswer { get; set; } = true;

			public List<String> Confirmations { get; } = [];

			public List<String> Errors { get; } = [];

			public Queue<String?> OpenAnswers { get; } = new();

			public Queue<String[]> OpenManyAnswers { get; } = new();

			public Queue<String?> SaveAnswers { get; } = new();

			public Task ShowInfoAsync(String title, String message)
			{
				return Task.CompletedTask;
			}

			public Task ShowWarningAsync(String title, String message)
			{
				return Task.CompletedTask;
			}

			public Task ShowErrorAsync(String title, String message)
			{
				this.Errors.Add(message);
				return Task.CompletedTask;
			}

			public Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
			{
				this.Confirmations.Add(message);
				return Task.FromResult(this.ConfirmAnswer);
			}

			public Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
			{
				return Task.FromResult(YesNoCancel.Yes);
			}

			public Task<String?> PromptAsync(String title, String message, String defaultValue = "")
			{
				return Task.FromResult<String?>(defaultValue);
			}

			public String? PickOpenFile(String title, String filter, String? initialDirectory = null)
			{
				return this.OpenAnswers.Count > 0 ? this.OpenAnswers.Dequeue() : null;
			}

			public IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null)
			{
				return this.OpenManyAnswers.Count > 0 ? this.OpenManyAnswers.Dequeue() : Array.Empty<String>();
			}

			public String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null)
			{
				return this.SaveAnswers.Count > 0 ? this.SaveAnswers.Dequeue() : null;
			}

			public String? PickFolder(String title, String? initialDirectory = null)
			{
				return null;
			}
		}

		private sealed class Fixture
		{
			public Fixture(Boolean withGerman = false)
			{
				this.Files.Files[English] = _english;
				this.Files.Files[French] = _french;
				this.Files.Siblings.Add(new MessageLanguageFile("french", French));
				if (withGerman)
				{
					this.Files.Files[German] = new[] { "{100}{}{Hallo.}", "{101}{}{Auf Wiedersehen.}", "{102}{}{Sie haben %d Kronen.}", "{103}{}{Danke.}", "{104}{}{Willkommen.}" };
					this.Files.Siblings.Add(new MessageLanguageFile("german", German));
				}

				this.ViewModel = new CompareMessagesViewModel(this.Files, this.Settings, this.Dialogs, this.Shell, this.Editors, this.Problems);
			}

			public FakeComparisonService Files { get; } = new();

			public FakeSettingsService Settings { get; } = new(@"C:\ss");

			public Dialogs Dialogs { get; } = new();

			public FakeShellService Shell { get; } = new();

			public FakeMessageEditorService Editors { get; } = new();

			public ProblemService Problems { get; } = new();

			public CompareMessagesViewModel ViewModel { get; }

			public CompareFilterViewModel Filter(MessageComparisonStatus status)
			{
				return this.ViewModel.Filters.Single(filter => filter.Status == status);
			}

			public Task OpenAsync()
			{
				return this.ViewModel.InitializeAsync(new CompareMessagesRequest(English));
			}
		}

		[Fact]
		public async Task The_other_languages_of_the_original_are_the_targets_and_the_first_one_is_compared()
		{
			Fixture fixture = new(withGerman: true);

			await fixture.OpenAsync();

			CompareMessagesViewModel viewModel = fixture.ViewModel;
			Assert.Equal(English, viewModel.ReferencePath);
			Assert.Equal("hakunin.msg", viewModel.ReferenceName);
			Assert.Equal(new[] { "french", "german" }, viewModel.Targets.Select(target => target.Language).ToArray());
			Assert.Same(viewModel.Targets[0], viewModel.SelectedTarget);
			Assert.Equal(French, viewModel.TargetPath);
			Assert.Contains((English, true), fixture.Files.Loads);
			Assert.Contains((French, false), fixture.Files.Loads);
			Assert.Contains((German, false), fixture.Files.Loads);
			Assert.Equal("Language: english - encoding: " + viewModel.Reference!.EncodingName, viewModel.ReferenceInfo);
			Assert.Equal("french - hakunin.msg - 4 to check", viewModel.Targets[0].DisplayName);
			Assert.Equal("german - hakunin.msg - 1 to check", viewModel.Targets[1].DisplayName);
		}

		[Fact]
		public async Task The_table_lists_the_lines_that_need_attention_and_the_summary_counts_every_status()
		{
			Fixture fixture = new();

			await fixture.OpenAsync();

			CompareMessagesViewModel viewModel = fixture.ViewModel;
			Assert.Equal(new[] { 101, 103, 104, 999 }, viewModel.Rows.Select(row => row.Number).ToArray());
			Assert.Equal("Same text (not translated)", viewModel.Rows.Single(row => row.Number == 103).StatusText);
			Assert.Equal("Empty in translation", viewModel.Rows.Single(row => row.Number == 104).StatusText);
			Assert.Equal("Extra in translation", viewModel.Rows.Single(row => row.Number == 999).StatusText);
			Assert.Equal("Player marker or description differs", viewModel.Rows.Single(row => row.Number == 101).StatusText);
			Assert.Equal(
				"6 lines: 0 missing, 1 extra, 1 not translated, 0 placeholders or tags only, 1 empty, 1 player marker or description, 2 OK; 3 with issues.",
				viewModel.SummaryText);
			Assert.Equal("4 line(s) shown.", viewModel.StatusText);
			Assert.Equal(1, fixture.Filter(MessageComparisonStatus.SameText).Count);
			Assert.Equal(2, fixture.Filter(MessageComparisonStatus.Ok).Count);
			Assert.Equal("Same text (not translated) (1)", fixture.Filter(MessageComparisonStatus.SameText).Label);
			Assert.False(fixture.Filter(MessageComparisonStatus.Ok).IsChecked);
		}

		[Fact]
		public async Task Each_filter_shows_or_hides_the_lines_of_its_status_and_the_counts_do_not_change()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();

			fixture.Filter(MessageComparisonStatus.Ok).IsChecked = true;
			Assert.Equal(new[] { 100, 101, 102, 103, 104, 999 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());

			fixture.Filter(MessageComparisonStatus.SameText).IsChecked = false;
			fixture.Filter(MessageComparisonStatus.ExtraInTarget).IsChecked = false;
			Assert.Equal(new[] { 100, 101, 102, 104 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());
			Assert.Equal(1, fixture.Filter(MessageComparisonStatus.SameText).Count);
			Assert.Equal("4 line(s) shown.", fixture.ViewModel.StatusText);
		}

		[Fact]
		public async Task Only_the_lines_with_issues_and_the_filter_box_narrow_the_table()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();
			fixture.Filter(MessageComparisonStatus.Ok).IsChecked = true;

			fixture.ViewModel.IssuesOnly = true;
			Assert.Equal(new[] { 101, 102, 104 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());
			Assert.Contains("Other %s or %d placeholders", fixture.ViewModel.Rows.Single(row => row.Number == 102).IssuesText, StringComparison.Ordinal);

			fixture.ViewModel.IssuesOnly = false;
			fixture.ViewModel.FilterText = "102";
			Assert.Equal(new[] { 102 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());

			fixture.ViewModel.FilterText = "revoir";
			Assert.Equal(new[] { 101 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());

			fixture.ViewModel.FilterText = "no such text";
			Assert.Empty(fixture.ViewModel.Rows);
			Assert.False(fixture.ViewModel.CopyNumbersCommand.CanExecute(null));
		}

		[Fact]
		public async Task A_row_names_its_number_its_status_and_its_issues_for_a_screen_reader()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();

			CompareRowViewModel row = fixture.ViewModel.Rows.Single(row => row.Number == 104);

			Assert.Equal("104, Empty in translation, Only white space:  ", row.AccessibleName);
			Assert.Equal("Welcome.", row.ReferenceText);
		}

		[Fact]
		public async Task Selecting_another_target_shows_its_comparison()
		{
			Fixture fixture = new(withGerman: true);
			await fixture.OpenAsync();

			fixture.ViewModel.SelectedTarget = fixture.ViewModel.Targets[1];

			Assert.Equal(German, fixture.ViewModel.TargetPath);
			Assert.Equal(new[] { 101 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());
		}

		[Fact]
		public async Task A_target_is_added_with_the_file_picker_once_and_removed_again()
		{
			Fixture fixture = new();
			fixture.Files.Files[German] = new[] { "{100}{}{Hallo.}" };
			await fixture.OpenAsync();
			fixture.Dialogs.OpenManyAnswers.Enqueue(new[] { German, French, English });

			await fixture.ViewModel.AddTargetCommand.ExecuteAsync(null);

			Assert.Equal(new[] { French, German }, fixture.ViewModel.Targets.Select(target => target.Path).ToArray());
			Assert.Same(fixture.ViewModel.Targets[1], fixture.ViewModel.SelectedTarget);
			Assert.Equal("german", fixture.ViewModel.Targets[1].Language);

			fixture.ViewModel.RemoveTargetCommand.Execute(null);

			Assert.Single(fixture.ViewModel.Targets);
			Assert.Same(fixture.ViewModel.Targets[0], fixture.ViewModel.SelectedTarget);
			fixture.ViewModel.RemoveTargetCommand.Execute(null);
			Assert.Empty(fixture.ViewModel.Targets);
			Assert.Null(fixture.ViewModel.SelectedTarget);
			Assert.Empty(fixture.ViewModel.Rows);
			Assert.Equal("No other language was found next to the original: add a translated message file.", fixture.ViewModel.StatusText);
		}

		[Fact]
		public async Task A_target_that_cannot_be_read_is_marked_and_the_others_still_compare()
		{
			Fixture fixture = new(withGerman: true);
			fixture.Files.Unreadable.Add(French);

			await fixture.OpenAsync();

			Assert.Equal("french - hakunin.msg - cannot be read", fixture.ViewModel.Targets[0].DisplayName);
			Assert.NotNull(fixture.ViewModel.Targets[0].Error);
			Assert.Equal("german - hakunin.msg - 1 to check", fixture.ViewModel.Targets[1].DisplayName);
		}

		[Fact]
		public async Task Without_an_original_that_can_be_read_every_target_says_so()
		{
			Fixture fixture = new();
			fixture.Files.Unreadable.Add(English);

			await fixture.OpenAsync();

			Assert.Null(fixture.ViewModel.Reference);
			Assert.Equal("french - hakunin.msg - cannot be read", fixture.ViewModel.Targets[0].DisplayName);
			Assert.StartsWith("The operation failed", fixture.ViewModel.StatusText, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Adding_the_missing_lines_asks_first_then_adds_the_original_messages_and_compares_again()
		{
			Fixture fixture = new();
			fixture.Files.Files[French] = new[] { "{100}{}{Bonjour.}", "{103}{}{Merci.}" };
			await fixture.OpenAsync();
			Assert.True(fixture.ViewModel.AddMissingCommand.CanExecute(null));

			fixture.Dialogs.ConfirmAnswer = false;
			await fixture.ViewModel.AddMissingCommand.ExecuteAsync(null);
			Assert.Empty(fixture.Files.Added);

			fixture.Dialogs.ConfirmAnswer = true;
			await fixture.ViewModel.AddMissingCommand.ExecuteAsync(null);

			(String path, Int32[] numbers, String? marker) = Assert.Single(fixture.Files.Added);
			Assert.Equal(French, path);
			Assert.Equal(new[] { 101, 102, 104 }, numbers);
			Assert.Null(marker);
			Assert.Equal("3 line(s) added to hakunin.msg.", fixture.ViewModel.StatusText);
			Assert.False(fixture.ViewModel.AddMissingCommand.CanExecute(null));
			Assert.Contains(fixture.Dialogs.Confirmations, text => text.Contains("3 missing line(s)", StringComparison.Ordinal) && text.Contains("hakunin.msg", StringComparison.Ordinal));
		}

		[Fact]
		public async Task The_marker_is_only_written_when_the_option_is_on()
		{
			Fixture fixture = new();
			fixture.Files.Files[French] = new[] { "{100}{}{Bonjour.}" };
			await fixture.OpenAsync();
			fixture.ViewModel.MarkUntranslated = true;

			await fixture.ViewModel.AddMissingCommand.ExecuteAsync(null);

			Assert.Equal("# TODO: translate", fixture.Files.Added.Single().Marker);
		}

		[Fact]
		public async Task Lines_that_the_encoding_cannot_hold_are_reported_after_the_others_were_added()
		{
			Fixture fixture = new();
			fixture.Files.Files[French] = new[] { "{100}{}{Bonjour.}" };
			fixture.Files.NotEncodable = new[] { 104 };
			await fixture.OpenAsync();

			await fixture.ViewModel.AddMissingCommand.ExecuteAsync(null);

			Assert.Equal("3 line(s) added to hakunin.msg; 1 could not be written in the encoding of the file.", fixture.ViewModel.StatusText);
		}

		[Fact]
		public async Task Nothing_can_be_added_when_the_translation_lacks_nothing()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();

			Assert.False(fixture.ViewModel.AddMissingCommand.CanExecute(null));
		}

		[Fact]
		public async Task The_numbers_of_the_listed_lines_are_copied()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();

			fixture.ViewModel.CopyNumbersCommand.Execute(null);

			Assert.Equal("101, 103, 104, 999", fixture.Shell.Clipboard);
			Assert.Equal("4 number(s) copied.", fixture.ViewModel.StatusText);
		}

		[Theory]
		[InlineData(@"C:\out\report.csv", true)]
		[InlineData(@"C:\out\report.txt", false)]
		public async Task The_report_is_a_csv_table_or_a_text_by_the_extension(String path, Boolean csv)
		{
			Fixture fixture = new();
			await fixture.OpenAsync();
			fixture.Dialogs.SaveAnswers.Enqueue(path);

			await fixture.ViewModel.ExportCommand.ExecuteAsync(null);

			(String written, String content) = Assert.Single(fixture.Files.Reports);
			Assert.Equal(path, written);
			if (csv)
			{
				Assert.StartsWith("No.,Status,Issues,Original text,Translated text\r\n", content, StringComparison.Ordinal);
				Assert.Contains("103,Same text (not translated),,Thanks.,Thanks.\r\n", content, StringComparison.Ordinal);
			}
			else
			{
				Assert.Contains("{103} Same text (not translated)\r\n\tOriginal text: Thanks.\r\n\tTranslated text: Thanks.\r\n", content, StringComparison.Ordinal);
			}

			Assert.Equal("4 line(s) written to " + System.IO.Path.GetFileName(path) + ".", fixture.ViewModel.StatusText);
		}

		[Fact]
		public async Task Cancelling_the_file_picker_exports_nothing()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();

			await fixture.ViewModel.ExportCommand.ExecuteAsync(null);

			Assert.Empty(fixture.Files.Reports);
		}

		[Fact]
		public async Task The_listed_lines_become_problems_of_the_message_source_with_a_place_in_the_files()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();
			fixture.Problems.Report(ProblemFactory.OperationFailed("something else"));

			fixture.ViewModel.ShowAsProblemsCommand.Execute(null);

			List<Problem> problems = fixture.Problems.Problems.Where(problem => problem.Origin == ProblemOrigins.MessageComparison).ToList();
			Assert.All(problems, problem => Assert.Equal(ProblemSource.Message, problem.Source));
			Problem same = Assert.Single(problems, problem => problem.MessageKey == "Str.Shell.Problem.CmpSame");
			Assert.Equal((French, 4), (same.File, same.Line));
			Assert.Equal("Message 103 of hakunin.msg has the same text as hakunin.msg: it is not translated.", same.Message);
			Problem empty = Assert.Single(problems, problem => problem.MessageKey == "Str.Shell.Problem.CmpEmpty");
			Assert.Equal(5, empty.Line);
			Assert.Contains(problems, problem => problem.MessageKey == "Str.Shell.Problem.CmpExtra" && problem.Line == 6);
			Assert.Contains(problems, problem => problem.MessageKey == "Str.Shell.Problem.CmpPlayer");
			Assert.Contains(problems, problem => problem.MessageKey == "Str.Shell.Problem.CmpIssue");
			Assert.Contains(fixture.Problems.Problems, problem => problem.MessageKey == "Str.Shell.Problem.OperationFailed");
			Assert.Equal("4 line(s) of hakunin.msg listed as problems.", fixture.ViewModel.StatusText);

			fixture.ViewModel.ShowAsProblemsCommand.Execute(null);
			Assert.Equal(problems.Count, fixture.Problems.Problems.Count(problem => problem.Origin == ProblemOrigins.MessageComparison));
		}

		[Fact]
		public async Task A_missing_message_points_to_the_line_of_the_original()
		{
			Fixture fixture = new();
			fixture.Files.Files[French] = new[] { "{100}{}{Bonjour.}" };
			await fixture.OpenAsync();

			fixture.ViewModel.ShowAsProblemsCommand.Execute(null);

			Problem missing = fixture.Problems.Problems.First(problem => problem.MessageKey == "Str.Shell.Problem.CmpMissing");
			Assert.Equal(English, missing.File);
			Assert.True(missing.Line > 0);
		}

		[Fact]
		public async Task At_most_500_lines_are_listed_as_problems()
		{
			Fixture fixture = new();
			fixture.Files.Files[English] = Enumerable.Range(1, 800).Select(i => "{" + i + "}{}{Text " + i + ".}").ToArray();
			fixture.Files.Files[French] = new[] { "{1}{}{Texte.}" };
			await fixture.OpenAsync();

			fixture.ViewModel.ShowAsProblemsCommand.Execute(null);

			Assert.Equal(CompareMessagesViewModel.MaxProblems, fixture.Problems.Problems.Count(problem => problem.Origin == ProblemOrigins.MessageComparison));
			Assert.StartsWith("The first 500 lines of hakunin.msg", fixture.ViewModel.StatusText, StringComparison.Ordinal);
		}

		[Fact]
		public async Task A_line_opens_in_the_message_editor_positioned_on_its_message()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();
			Assert.False(fixture.ViewModel.OpenRowCommand.CanExecute(null));

			fixture.ViewModel.SelectedRow = fixture.ViewModel.Rows.Single(row => row.Number == 103);
			fixture.ViewModel.OpenRowCommand.Execute(null);
			fixture.ViewModel.OpenReferenceRowCommand.Execute(null);

			Assert.Equal(2, fixture.Editors.Options.Count);
			Assert.Equal((French, 103), (fixture.Editors.Options[0].FilePath, fixture.Editors.Options[0].SelectMessageId));
			Assert.Equal((English, 103), (fixture.Editors.Options[1].FilePath, fixture.Editors.Options[1].SelectMessageId));
		}

		[Fact]
		public async Task A_message_the_translation_lacks_opens_in_the_original()
		{
			Fixture fixture = new();
			fixture.Files.Files[French] = new[] { "{100}{}{Bonjour.}" };
			await fixture.OpenAsync();
			fixture.ViewModel.SelectedRow = fixture.ViewModel.Rows.First(row => row.Status == MessageComparisonStatus.MissingInTarget);

			fixture.ViewModel.OpenRowCommand.Execute(null);

			Assert.Equal(English, fixture.Editors.Options.Single().FilePath);
		}

		[Fact]
		public async Task Another_original_is_chosen_with_the_file_picker_and_its_languages_are_offered()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();
			String other = @"C:\game\text\english\dialog\other.msg";
			fixture.Files.Files[other] = new[] { "{1}{}{One.}" };
			fixture.Files.Siblings.Clear();
			fixture.Dialogs.OpenAnswers.Enqueue(other);

			await fixture.ViewModel.ChooseReferenceCommand.ExecuteAsync(null);

			Assert.Equal(other, fixture.ViewModel.ReferencePath);
			Assert.Empty(fixture.ViewModel.Targets);
			Assert.Null(fixture.ViewModel.SelectedTarget);
		}

		[Fact]
		public async Task Comparing_again_reads_the_files_again()
		{
			Fixture fixture = new();
			await fixture.OpenAsync();
			fixture.Files.Files[French] = new[] { "{100}{}{Bonjour.}", "{101}{}{Au revoir.}", "{102}{}{Vous avez %d caps.}", "{103}{}{Merci.}", "{104}{}{Bienvenue.}" };

			await fixture.ViewModel.CompareCommand.ExecuteAsync(null);

			Assert.Equal(new[] { 101 }, fixture.ViewModel.Rows.Select(row => row.Number).ToArray());
		}
	}
}