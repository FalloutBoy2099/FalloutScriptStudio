namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Tests.Flowchart;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using ICSharpCode.AvalonEdit.Document;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public sealed class ScriptToolServiceTests : IDisposable
	{
		private readonly ScriptToolsTempDirectory _temp = new ScriptToolsTempDirectory();
		private readonly FakeDialogService _dialogs = new FakeDialogService();
		private readonly FakeWorkspace _workspace = new FakeWorkspace();
		private readonly FakeNodeCodeEditorService _nodeEditors = new FakeNodeCodeEditorService();
		private readonly ServiceProvider _provider;

		public ScriptToolServiceTests()
		{
			ServiceCollection services = new ServiceCollection();
			services.AddSingleton<ISettingsService>(new FakeSettingsService(this._temp.Path));
			services.AddSingleton<IDialogService>(this._dialogs);
			services.AddSingleton(this._workspace.Interface);
			services.AddSingleton<INodeCodeEditorService>(this._nodeEditors);
			services.AddScriptTools();
			this._provider = services.BuildServiceProvider();
		}

		public void Dispose()
		{
			this._provider.Dispose();
			this._temp.Dispose();
		}

		private static DocumentViewModel Script(String? path)
		{
			return new DocumentViewModel(path, DocumentViewModel.KindFromPath(path), new TextDocument());
		}

		[Fact]
		public async Task CloseToolWindows_ClosesTheNodeEditorsOfTheScript()
		{
			IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

			await service.CloseToolWindowsAsync(Script("C:\\scripts\\npc.ssl"));

			Assert.Equal(1, this._nodeEditors.CloseAllCount);
		}

		[Fact]
		public async Task CloseToolWindows_WithoutOpenWindows_DoesNothingElse()
		{
			IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

			await service.CloseToolWindowsAsync(Script(null));

			Assert.Empty(this._dialogs.Errors);
		}

		[Fact]
		public void AddScriptTools_RegistersTheServiceAndTheViewModels()
		{
			Assert.IsType<ScriptToolService>(this._provider.GetRequiredService<IScriptToolService>());
			Assert.Same(this._provider.GetRequiredService<IScriptToolService>(), this._provider.GetRequiredService<IScriptToolService>());
			Assert.NotSame(this._provider.GetRequiredService<ProcedureNameViewModel>(), this._provider.GetRequiredService<ProcedureNameViewModel>());
			Assert.NotNull(this._provider.GetRequiredService<RegisterScriptViewModel>());
			Assert.NotNull(this._provider.GetRequiredService<AdvancedSearchViewModel>());
		}

		[Fact]
		public async Task ShowRegisterScript_UnsavedScript_ShowsAnErrorAndOpensNothing()
		{
			IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

			await service.ShowRegisterScriptAsync(Script(null));

			Assert.Contains("unsaved", Assert.Single(this._dialogs.Errors), StringComparison.Ordinal);
		}

		[Fact]
		public async Task ShowRegisterScript_NameTooLong_ShowsAnError()
		{
			IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

			await service.ShowRegisterScriptAsync(Script("C:\\scripts\\muchtoolongname.ssl"));

			Assert.Contains("8 characters", Assert.Single(this._dialogs.Errors), StringComparison.Ordinal);
		}

		[Fact]
		public async Task ShowRegisterScript_GlobalScript_AsksAndStopsWhenDeclined()
		{
			this._dialogs.ConfirmAnswer = false;
			IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

			await service.ShowRegisterScriptAsync(Script("C:\\scripts\\glboom.ssl"));

			Assert.Equal(1, this._dialogs.ConfirmCount);
			Assert.Empty(this._dialogs.Errors);
		}

		[Fact]
		public void ShowRegisterScript_WithoutScriptsFolder_ShowsTheConfigurationError()
		{
			StaTestRunner.Run(async () =>
			{
				IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

				await service.ShowRegisterScriptAsync(null);

				Assert.Single(this._dialogs.Errors);
			});
		}

		[Fact]
		public void ShowRegisterScript_CalledTwiceWhileTheFirstIsStillOpening_PreparesOnlyOneWindow()
		{
			StaTestRunner.Run(async () =>
			{
				TaskCompletionSource gate = new TaskCompletionSource();
				this._dialogs.ErrorGate = gate.Task;
				IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

				Task first = service.ShowRegisterScriptAsync(null);
				Task second = service.ShowRegisterScriptAsync(null);
				gate.SetResult();
				await Task.WhenAll(first, second);

				Assert.Single(this._dialogs.Errors);
			});
		}

		[Fact]
		public async Task Flowchart_OfAScriptThatWasNeverSaved_AsksToSaveItFirst()
		{
			IScriptToolService service = this._provider.GetRequiredService<IScriptToolService>();

			await service.ShowFlowchartAsync(Script(null));

			Assert.Single(this._dialogs.Infos);
		}

		[Fact]
		public void AdvancedSearch_HistoryRefresh_PicksUpQueriesFromTheQuickSearch()
		{
			StaTestRunner.Run(() =>
			{
				FakeSettingsService settings = new FakeSettingsService(this._temp.Path);
				AdvancedSearchViewModel viewModel = new AdvancedSearchViewModel(this._workspace.Interface, settings, this._dialogs, new FalloutScriptStudio.Services.ScriptTools.Search.ScriptSearcher());
				viewModel.SearchText = "typed";

				settings.SearchHistory.AddQuery("from quick panel");
				viewModel.RefreshHistory();

				Assert.Equal(new[] { "from quick panel" }, viewModel.QueryHistory);
				Assert.Equal("typed", viewModel.SearchText);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void AdvancedSearch_SavedFolders_StaySorted()
		{
			StaTestRunner.Run(() =>
			{
				FakeSettingsService settings = new FakeSettingsService(this._temp.Path);
				settings.SearchHistory.Directories.AddRange(new[] { "C:\\b", "C:\\d" });
				AdvancedSearchViewModel viewModel = new AdvancedSearchViewModel(this._workspace.Interface, settings, this._dialogs, new FalloutScriptStudio.Services.ScriptTools.Search.ScriptSearcher());
				this._dialogs.FolderAnswers.Enqueue("C:\\c");
				this._dialogs.FolderAnswers.Enqueue("C:\\a");

				viewModel.BrowseFolderCommand.Execute(null);
				viewModel.BrowseFolderCommand.Execute(null);

				Assert.Equal(new[] { "C:\\a", "C:\\b", "C:\\c", "C:\\d" }, viewModel.Folders);
				return Task.CompletedTask;
			});
		}
	}
}
