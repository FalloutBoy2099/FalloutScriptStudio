namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Commands;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	/// <summary>"Undo Last Replace in Files" is disabled while there is nothing to undo and enables itself right after a replacement.</summary>
	public sealed class UndoReplaceStateTests
	{
		private sealed class FakeReplaceService : IReplaceInFilesService
		{
			private Boolean _canUndo;

			public event EventHandler? UndoStateChanged;

			public Boolean CanUndo
			{
				get => this._canUndo;
				set
				{
					this._canUndo = value;
					this.UndoStateChanged?.Invoke(this, EventArgs.Empty);
				}
			}

			public Int32 Subscribers => this.UndoStateChanged?.GetInvocationList().Length ?? 0;

			public Task<ReplaceOutcome> RunAsync(ReplaceInFilesRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
			{
				throw new NotSupportedException();
			}

			public Task<ReplaceUndoResult?> UndoLastAsync(CancellationToken cancellationToken)
			{
				throw new NotSupportedException();
			}
		}

		private static CommandEntry UndoEntry(ShellHarness harness)
		{
			return CommandCatalog.Create(harness.Main).Single(entry => entry.Id == "UndoReplaceInFilesCommand");
		}

		[Fact]
		public void TheCommandIsDisabledWhenThereIsNothingToUndo_AndThePaletteSaysWhy()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();

				Assert.False(harness.Main.CanUndoReplaceInFiles);
				Assert.False(harness.Main.UndoReplaceInFilesCommand.CanExecute(null));
				Assert.Equal(CommandAvailability.NothingToUndo, UndoEntry(harness).Availability);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void TheCommandEnablesItselfRightAfterAReplacement_AndDisablesAfterTheUndo()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				Int32 canExecuteChanged = 0;
				harness.Main.UndoReplaceInFilesCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;
				System.Collections.Generic.List<String> changed = [];
				harness.Main.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);

				harness.ScriptTools.CanUndoReplaceInFiles = true;
				harness.ScriptTools.RaiseUndoReplaceInFilesStateChanged();

				Assert.True(harness.Main.CanUndoReplaceInFiles);
				Assert.True(harness.Main.UndoReplaceInFilesCommand.CanExecute(null));
				Assert.Equal(CommandAvailability.Available, UndoEntry(harness).Availability);
				Assert.True(canExecuteChanged > 0);
				Assert.Contains(nameof(harness.Main.CanUndoReplaceInFiles), changed);

				harness.ScriptTools.CanUndoReplaceInFiles = false;
				harness.ScriptTools.RaiseUndoReplaceInFilesStateChanged();

				Assert.False(harness.Main.UndoReplaceInFilesCommand.CanExecute(null));
				Assert.Equal(CommandAvailability.NothingToUndo, UndoEntry(harness).Availability);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void TheScriptToolServiceForwardsTheStateOfTheReplaceService()
		{
			FakeReplaceService replace = new FakeReplaceService();
			ServiceCollection services = new ServiceCollection();
			services.AddSingleton<IDialogService>(new FalloutScriptStudio.Tests.ScriptTools.FakeDialogService());
			services.AddSingleton<IReplaceInFilesService>(replace);
			services.AddSingleton<IScriptToolService, ScriptToolService>();
			using ServiceProvider provider = services.BuildServiceProvider();
			IScriptToolService tools = provider.GetRequiredService<IScriptToolService>();
			Int32 raised = 0;
			EventHandler handler = (_, _) => raised++;

			Assert.False(tools.CanUndoReplaceInFiles);
			tools.UndoReplaceInFilesStateChanged += handler;
			replace.CanUndo = true;

			Assert.True(tools.CanUndoReplaceInFiles);
			Assert.Equal(1, raised);

			tools.UndoReplaceInFilesStateChanged -= handler;
			replace.CanUndo = false;

			Assert.False(tools.CanUndoReplaceInFiles);
			Assert.Equal(1, raised);
			Assert.Equal(0, replace.Subscribers);
		}

		[Fact]
		public void AServiceWithoutTheStateSaysThereIsNothingToUndo()
		{
			IScriptToolService tools = new NoStateTools();

			Assert.False(tools.CanUndoReplaceInFiles);
			tools.UndoReplaceInFilesStateChanged += (_, _) => { };
		}

		private sealed class NoStateTools : IScriptToolService
		{
			public Task<String?> PromptProcedureNameAsync(String initialName, FalloutScriptStudio.Services.ScriptTools.ProcedureNameDialogMode mode, System.Collections.Generic.IReadOnlyCollection<String> existingNames) => throw new NotSupportedException();

			public Task<FalloutScriptStudio.Services.ScriptTools.Procedures.ProcedureNameResult?> PromptProcedureAsync(FalloutScriptStudio.Services.ScriptTools.Procedures.ProcedureNameRequest request) => throw new NotSupportedException();

			public Task ShowRegisterScriptAsync(FalloutScriptStudio.Documents.DocumentViewModel? script) => throw new NotSupportedException();

			public Task ShowAdvancedSearchAsync(String? initialText) => throw new NotSupportedException();

			public Task ShowFlowchartAsync(FalloutScriptStudio.Documents.DocumentViewModel script) => throw new NotSupportedException();

			public Task ShowDialoguePreviewAsync(FalloutScriptStudio.Documents.DocumentViewModel script) => throw new NotSupportedException();

			public Task ShowDialoguePreviewAsync(FalloutScriptStudio.Documents.DocumentViewModel script, FalloutScriptStudio.Services.ScriptTools.DialoguePreviewOpenOptions options) => throw new NotSupportedException();

			public Task ShowFunctionRulesAsync() => throw new NotSupportedException();

			public Task CloseToolWindowsAsync(FalloutScriptStudio.Documents.DocumentViewModel script) => throw new NotSupportedException();
		}
	}
}