namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class WorkspaceToolWindowsTests
	{
		private const String Script = "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n";

		[Fact]
		public void Closing_a_tab_closes_the_flowchart_and_node_editors_of_the_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				Boolean closed = await harness.Workspace.CloseAsync(document);

				Assert.True(closed);
				Assert.Equal(new[] { "closeWindows:a.ssl" }, harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void The_tool_windows_close_before_the_unsaved_changes_question()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				Boolean closed = await harness.Workspace.CloseAsync(document);

				Assert.False(closed);
				Assert.Equal(new[] { "closeWindows:a.ssl" }, harness.ScriptTools.Calls);
				Assert.Contains(document, harness.Workspace.Documents);
			});
		}

		[Fact]
		public void Closing_all_tabs_closes_the_tool_windows_of_each_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);

				await harness.Workspace.CloseAllAsync();

				Assert.Equal(new[] { "closeWindows:b.ssl", "closeWindows:a.ssl" }, harness.ScriptTools.Calls);
				Assert.Empty(harness.Workspace.Documents);
			});
		}
	}
}
