namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class RegisterScriptViewModelTests : IDisposable
	{
		private readonly ScriptToolsTempDirectory _temp = new ScriptToolsTempDirectory();
		private readonly FakeSettingsService _settings;
		private readonly FakeDialogService _dialogs = new FakeDialogService();
		private readonly FakeWorkspace _workspace = new FakeWorkspace();
		private readonly FakeDatExtractor _extractor = new FakeDatExtractor();
		private readonly String _scriptsDirectory;
		private readonly String _listPath;
		private readonly String _namesPath;
		private readonly String _headerPath;

		public RegisterScriptViewModelTests()
		{
			this._settings = new FakeSettingsService(this._temp.Combine("app"));
			this._listPath = this._temp.Write(
				"game/data/scripts/scripts.lst",
				new ScriptListEntry { Script = "first.int", Description = "First script", LocalVars = 2 }.ToListLine() + "\r\n"
				+ new ScriptListEntry { Script = "second.int", Description = "Second script", LocalVars = 0 }.ToListLine() + "\r\n");
			this._scriptsDirectory = Path.GetDirectoryName(this._listPath)!;
			this._namesPath = this._temp.Write("game/data/text/english/game/scrname.msg", "#\r\n{101}{}{Alpha}\r\n{102}{}{Beta}\r\n");
			this._headerPath = this._temp.Write("headers/scripts.h", "#ifndef SCRIPTS_H\r\n#define SCRIPT_FIRST (1)\r\n#endif\r\n");
			this._settings.Settings.Paths.OutputDirectory = this._scriptsDirectory;
			this._settings.Settings.Paths.ScriptsHeaderFile = this._headerPath;
		}

		public void Dispose()
		{
			this._temp.Dispose();
		}

		private RegisterScriptViewModel CreateViewModel(Boolean withExtractor = false)
		{
			return new RegisterScriptViewModel(this._settings, this._dialogs, new ScriptRegistryStore(), this._workspace.Interface, withExtractor ? new ArchiveScriptExtractor(this._settings, this._extractor, this._dialogs) : null);
		}

		[Fact]
		public async Task Initialize_WithoutScript_ShowsTheListAndNothingIsModified()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();

			Boolean opened = await viewModel.InitializeAsync(null);

			Assert.True(opened);
			Assert.Equal(new[] { "first.int", "second.int" }, viewModel.Entries.Select(entry => entry.Script).ToArray());
			Assert.Equal(new[] { 1, 2 }, viewModel.Entries.Select(entry => entry.Number).ToArray());
			Assert.Equal("Alpha", viewModel.Entries[0].GameName);
			Assert.False(viewModel.IsModified);
			Assert.False(viewModel.CanDefine);
			Assert.Equal("All changes saved", viewModel.StatusText);
			Assert.Empty(this._dialogs.Warnings);
		}

		[Fact]
		public async Task Initialize_WithoutScriptsFolder_ShowsAnError()
		{
			this._settings.Settings.Paths.OutputDirectory = null;
			RegisterScriptViewModel viewModel = this.CreateViewModel();

			Boolean opened = await viewModel.InitializeAsync(null);

			Assert.False(opened);
			Assert.Single(this._dialogs.Errors);
		}

		[Fact]
		public async Task Initialize_WithoutList_ShowsAnError()
		{
			File.Delete(this._listPath);
			RegisterScriptViewModel viewModel = this.CreateViewModel();

			Boolean opened = await viewModel.InitializeAsync(null);

			Assert.False(opened);
			Assert.Contains("scripts.lst", Assert.Single(this._dialogs.Errors), StringComparison.Ordinal);
		}

		[Fact]
		public async Task Initialize_MissingNameFile_WarnsOnlyWhenWarningsAreEnabled()
		{
			File.Delete(this._namesPath);
			await this.CreateViewModel().InitializeAsync(null);
			Assert.Single(this._dialogs.Warnings);

			this._dialogs.Warnings.Clear();
			this._settings.Settings.Compiler.ShowWarnings = false;
			await this.CreateViewModel().InitializeAsync(null);
			Assert.Empty(this._dialogs.Warnings);
		}

		[Fact]
		public async Task Initialize_NewScript_AddsARowAndPreparesTheDefinition()
		{
			this._settings.Settings.Messages.RegisterScriptAllowDefine = true;
			RegisterScriptViewModel viewModel = this.CreateViewModel();

			await viewModel.InitializeAsync("newone.int");

			Assert.Equal(3, viewModel.Entries.Count);
			Assert.Equal("newone.int", viewModel.SelectedEntry!.Script);
			Assert.Equal(3, viewModel.SelectedEntry.Number);
			Assert.True(viewModel.IsModified);
			Assert.True(viewModel.CanDefine);
			Assert.True(viewModel.AllowDefine);
			Assert.Equal("SCRIPT_NEWONE", viewModel.DefineName);
			Assert.Equal("Unsaved changes", viewModel.StatusText);
		}

		[Fact]
		public async Task Initialize_AlreadyRegisteredScript_SelectsItWithoutChanges()
		{
			this._settings.Settings.Messages.RegisterScriptAllowDefine = true;
			RegisterScriptViewModel viewModel = this.CreateViewModel();

			await viewModel.InitializeAsync("SECOND.int");

			Assert.Equal(2, viewModel.Entries.Count);
			Assert.Equal("second.int", viewModel.SelectedEntry!.Script);
			Assert.False(viewModel.IsModified);
			Assert.True(viewModel.CanDefine);
			Assert.False(viewModel.AllowDefine);
		}

		[Fact]
		public async Task Save_WritesTheListNamesAndTheDefinition()
		{
			this._settings.Settings.Messages.RegisterScriptAllowDefine = true;
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("newone.int");
			viewModel.SelectedEntry!.Description = "New script";
			viewModel.SelectedEntry.GameName = "Gamma";
			viewModel.SelectedEntry.LocalVars = 4;

			await viewModel.SaveCommand.ExecuteAsync(null);

			String[] lines = File.ReadAllLines(this._listPath);
			Assert.Equal(3, lines.Length);
			Assert.Equal("newone.int".PadRight(16) + "; " + "New script".PadRight(45) + " # local_vars=4", lines[2]);
			String names = File.ReadAllText(this._namesPath);
			Assert.Contains("{103}{}{Gamma}", names, StringComparison.Ordinal);
			Assert.Contains("{101}{}{Alpha}", names, StringComparison.Ordinal);
			String header = File.ReadAllText(this._headerPath);
			Assert.Contains("#define SCRIPT_NEWONE", header, StringComparison.Ordinal);
			Assert.Contains("(3)", header, StringComparison.Ordinal);
			Assert.True(header.IndexOf("#define SCRIPT_NEWONE", StringComparison.Ordinal) < header.IndexOf("#endif", StringComparison.Ordinal));
			Assert.False(viewModel.IsModified);
			Assert.True(viewModel.HasNotice);
			Assert.Empty(this._dialogs.Errors);
		}

		[Fact]
		public async Task Save_AllowOff_DoesNotTouchTheHeader()
		{
			String before = File.ReadAllText(this._headerPath);
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("newone.int");
			viewModel.AllowDefine = false;

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.Equal(before, File.ReadAllText(this._headerPath));
			Assert.Equal(3, File.ReadAllLines(this._listPath).Length);
		}

		[Fact]
		public async Task Save_DefinitionAlreadyThere_IsReportedNotDuplicated()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("first.int");
			viewModel.AllowDefine = true;
			viewModel.DefineName = "script_first";
			viewModel.SelectedEntry!.Description = "Edited";

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.Equal(1, File.ReadAllText(this._headerPath).Split("SCRIPT_FIRST").Length - 1);
			Assert.Contains("already exists", viewModel.NoticeMessage, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Save_WithoutHeaderFile_SavesTheListAndReportsTheProblem()
		{
			this._settings.Settings.Paths.ScriptsHeaderFile = null;
			this._settings.Settings.Compiler.ShowWarnings = false;
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("newone.int");
			viewModel.AllowDefine = true;

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.Equal(3, File.ReadAllLines(this._listPath).Length);
			Assert.Contains("scripts.h could not be found", Assert.Single(this._dialogs.Errors), StringComparison.Ordinal);
			Assert.False(viewModel.IsModified);
		}

		[Fact]
		public async Task Save_InvalidDefinitionName_SavesTheListAndReportsTheProblem()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("newone.int");
			viewModel.AllowDefine = true;
			viewModel.DefineName = "not valid";

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.Equal(3, File.ReadAllLines(this._listPath).Length);
			Assert.Contains("not a valid name", Assert.Single(this._dialogs.Errors), StringComparison.Ordinal);
			Assert.DoesNotContain("not valid", File.ReadAllText(this._headerPath), StringComparison.Ordinal);
		}

		[Fact]
		public async Task TogglingAllow_UpdatesTheSetting()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("newone.int");

			viewModel.AllowDefine = true;
			Boolean turnedOn = this._settings.Settings.Messages.RegisterScriptAllowDefine;
			viewModel.AllowDefine = false;

			Assert.True(turnedOn);
			Assert.False(this._settings.Settings.Messages.RegisterScriptAllowDefine);
		}

		[Fact]
		public async Task EditingARow_MarksTheListAsModified()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			viewModel.Entries[0].Description = "Changed";

			Assert.True(viewModel.IsModified);
			Assert.True(viewModel.SaveCommand.CanExecute(null));
		}

		[Fact]
		public async Task EditingARow_WithTheSameValue_DoesNotModify()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			viewModel.Entries[0].Description = "First script";
			viewModel.Entries[0].Script = "first.int";

			Assert.False(viewModel.IsModified);
			Assert.False(viewModel.SaveCommand.CanExecute(null));
		}

		[Fact]
		public async Task EditingScriptName_TooLong_IsRefusedWithAMessage()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			viewModel.Entries[0].Script = "waytoolongname.int";

			Assert.Equal("first.int", viewModel.Entries[0].Script);
			Assert.Contains("at most 8", viewModel.NoticeMessage, StringComparison.Ordinal);
			Assert.False(viewModel.IsModified);
		}

		[Fact]
		public async Task EditingGameName_WithBraces_IsRefused()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			viewModel.Entries[0].GameName = "Bad{name}";

			Assert.Equal("Alpha", viewModel.Entries[0].GameName);
			Assert.True(viewModel.HasNotice);
		}

		[Fact]
		public async Task AddRow_AppendsANoneRow_AndRemoveLastRowRemovesIt()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			viewModel.AddRowCommand.Execute(null);

			Assert.Equal(3, viewModel.Entries.Count);
			Assert.Equal("none.int", viewModel.Entries[2].Script);
			Assert.Same(viewModel.Entries[2], viewModel.SelectedEntry);
			Assert.True(viewModel.IsModified);

			viewModel.RemoveLastRowCommand.Execute(null);

			Assert.Equal(2, viewModel.Entries.Count);
			Assert.Same(viewModel.Entries[1], viewModel.SelectedEntry);
		}

		[Fact]
		public async Task RemovingTheRegisteredRow_DisablesTheDefinition()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync("newone.int");

			viewModel.RemoveLastRowCommand.Execute(null);

			Assert.False(viewModel.CanDefine);
			Assert.False(viewModel.AllowDefine);
		}

		[Fact]
		public async Task Find_MovesTheSelectionAndWrapsAround()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);
			viewModel.FindText = "script";

			viewModel.FindNextCommand.Execute(null);
			ScriptEntryViewModel? first = viewModel.SelectedEntry;
			Int32 firstColumn = viewModel.FindColumn;
			viewModel.FindNextCommand.Execute(null);
			ScriptEntryViewModel? second = viewModel.SelectedEntry;
			viewModel.FindNextCommand.Execute(null);
			ScriptEntryViewModel? wrapped = viewModel.SelectedEntry;
			viewModel.FindPreviousCommand.Execute(null);

			Assert.Equal("first.int", first!.Script);
			Assert.Equal(2, firstColumn);
			Assert.Equal("second.int", second!.Script);
			Assert.Same(first, wrapped);
			Assert.Same(second, viewModel.SelectedEntry);
			viewModel.FindText = "nothing like this";
			viewModel.FindNextCommand.Execute(null);
			Assert.Equal("Nothing found.", viewModel.NoticeMessage);
		}

		[Fact]
		public async Task ConfirmClose_UnmodifiedList_Closes()
		{
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			Assert.True(await viewModel.ConfirmCloseAsync());
		}

		[Theory]
		[InlineData(YesNoCancel.Yes, true, true)]
		[InlineData(YesNoCancel.No, true, false)]
		[InlineData(YesNoCancel.Cancel, false, false)]
		public async Task ConfirmClose_ModifiedList_FollowsTheAnswer(YesNoCancel answer, Boolean expectedClose, Boolean expectedSaved)
		{
			this._dialogs.YesNoCancelAnswer = answer;
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);
			viewModel.Entries[0].Description = "Changed on purpose";

			Boolean close = await viewModel.ConfirmCloseAsync();

			Assert.Equal(expectedClose, close);
			Assert.Equal(expectedSaved, File.ReadAllText(this._listPath).Contains("Changed on purpose", StringComparison.Ordinal));
		}

		[Fact]
		public async Task OpenScript_ExistingFile_OpensItInTheWorkspace()
		{
			String script = this._temp.Write("game/data/scripts/first.int", "binary");
			RegisterScriptViewModel viewModel = this.CreateViewModel();
			await viewModel.InitializeAsync(null);

			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[0]);

			Assert.Equal(script, Assert.Single(this._workspace.Opened));
		}

		[Fact]
		public async Task OpenScript_MissingFile_IsExtractedFromTheArchiveAndTheArchiveIsRemembered()
		{
			String dat = this._temp.Write("game/master.dat", "dat");
			this._dialogs.FileAnswers.Enqueue(dat);
			RegisterScriptViewModel viewModel = this.CreateViewModel(true);
			await viewModel.InitializeAsync(null);

			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[0]);
			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[1]);

			Assert.Equal(new[] { "first.int", "second.int" }, this._extractor.Requests.Select(request => request.ScriptFileName).ToArray());
			Assert.All(this._extractor.Requests, request => Assert.Equal(dat, request.DatFilePath));
			Assert.Equal(2, this._workspace.Opened.Count);
			Assert.Equal(this._settings.Paths.ScratchDirectory, this._extractor.Requests[0].OutputDirectory);
		}

		[Fact]
		public async Task OpenScript_UsesTheArchiveThatAnotherCallerOfTheSharedExtractorAlreadyChose()
		{
			String dat = this._temp.Write("game/master.dat", "dat");
			this._dialogs.FileAnswers.Enqueue(dat);
			ArchiveScriptExtractor shared = new ArchiveScriptExtractor(this._settings, this._extractor, this._dialogs);
			RegisterScriptViewModel viewModel = new RegisterScriptViewModel(this._settings, this._dialogs, new ScriptRegistryStore(), this._workspace.Interface, shared);
			await viewModel.InitializeAsync(null);

			// For example "open from archive" in the main window.
			Assert.NotNull(await shared.ExtractAsync("other.int"));
			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[0]);

			Assert.Equal(new[] { "other.int", "first.int" }, this._extractor.Requests.Select(request => request.ScriptFileName).ToArray());
			Assert.All(this._extractor.Requests, request => Assert.Equal(dat, request.DatFilePath));
			Assert.Empty(this._dialogs.FileAnswers);
			Assert.Single(this._workspace.Opened);
		}

		[Fact]
		public async Task OpenScript_ExtractionFails_ShowsAnErrorAndAsksForTheArchiveAgain()
		{
			String dat = this._temp.Write("game/master.dat", "dat");
			this._dialogs.FileAnswers.Enqueue(dat);
			this._dialogs.FileAnswers.Enqueue(dat);
			this._extractor.Fail = true;
			RegisterScriptViewModel viewModel = this.CreateViewModel(true);
			await viewModel.InitializeAsync(null);

			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[0]);
			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[0]);

			Assert.Equal(2, this._dialogs.Errors.Count);
			Assert.Empty(this._workspace.Opened);
			Assert.Empty(this._dialogs.FileAnswers);
		}

		[Fact]
		public async Task OpenScript_NoArchiveChosen_DoesNothing()
		{
			this._dialogs.FileAnswers.Enqueue(null);
			RegisterScriptViewModel viewModel = this.CreateViewModel(true);
			await viewModel.InitializeAsync(null);

			await viewModel.OpenScriptCommand.ExecuteAsync(viewModel.Entries[0]);

			Assert.Empty(this._extractor.Requests);
			Assert.Empty(this._dialogs.Errors);
		}

		private sealed class FakeDatExtractor : IDatArchiveExtractor
		{
			public System.Collections.Generic.List<DatExtractionRequest> Requests { get; } = [];

			public Boolean Fail { get; set; }

			public Task<DatExtractionResult> ExtractScriptAsync(DatExtractionRequest request, CancellationToken cancellationToken = default)
			{
				this.Requests.Add(request);
				if (this.Fail)
				{
					return Task.FromResult(new DatExtractionResult(false, null, "boom"));
				}

				String path = Path.Combine(request.OutputDirectory, request.ScriptFileName);
				return Task.FromResult(new DatExtractionResult(true, path, String.Empty));
			}

			public String BuildCommandLine(DatExtractionRequest request)
			{
				return String.Empty;
			}
		}
	}
}