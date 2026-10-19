namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class MainViewModelReplaceTests
	{
		[Fact]
		public void TheEditMenuCommandOpensTheSearchWindowReadyToReplaceWithTheWordAtTheCaret()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\nend\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.WordAtCaret = "start";

				await harness.Main.ReplaceInFilesCommand.ExecuteAsync(null);

				Assert.Contains("replaceInFiles:start", harness.ScriptTools.Calls);
				Assert.DoesNotContain("search:start", harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void TheUndoCommandAsksTheScriptToolsToUndoTheLastRun()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.UndoReplaceInFilesCommand.ExecuteAsync(null);

				Assert.Contains("undoReplaceInFiles", harness.ScriptTools.Calls);
			});
		}
	}
}