namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>"Format on save" and "Auto Formatting Code" use the style in effect.</summary>
	public sealed class FormatOnSaveTests
	{
		private const String Messy = "procedure start begin\r\nvariable x:=1+2;\r\nend\r\n";

		[Fact]
		public void FormatOnSave_IsOffByDefault_AndTheTextIsSavedAsItIs()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Messy);
				document.TextDocument.Insert(0, "// edit\r\n");

				await harness.Workspace.SaveAsync(document);

				Assert.Equal("// edit\r\n" + Messy, File.ReadAllText(document.FilePath!));
			});
		}

		[Fact]
		public void FormatOnSave_FormatsAScriptBeforeItIsWritten_AndTheEditorShowsWhatWasSaved()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings =>
				{
					settings.Editor.Formatting.FormatOnSave = true;
					settings.Editor.Formatting.Reindent = true;
				});
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Messy);
				document.TextDocument.Insert(0, "// edit\r\n");

				Boolean saved = await harness.Workspace.SaveAsync(document);

				String expected = "// edit\r\nprocedure start begin\r\n   variable x := 1 + 2;\r\nend\r\n";
				Assert.True(saved);
				Assert.Equal(expected, File.ReadAllText(document.FilePath!));
				Assert.Equal(expected, document.TextDocument.Text);
				Assert.False(document.IsModified);
			});
		}

		[Fact]
		public void FormatOnSave_IsOneUndoStep_ThatBringsBackTheUnformattedText()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.Formatting.FormatOnSave = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.h", Messy);
				document.TextDocument.Insert(0, "// edit\r\n");
				await harness.Workspace.SaveAsync(document);

				document.TextDocument.UndoStack.Undo();

				Assert.Equal("// edit\r\n" + Messy, document.TextDocument.Text);
			});
		}

		[Fact]
		public void FormatOnSave_LeavesMessageFilesAndOtherFilesAlone()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.Formatting.FormatOnSave = true);
				DocumentViewModel message = await harness.OpenFileAsync("a.msg", "{100}{}{a+b,c:=d}\r\n");
				DocumentViewModel other = await harness.OpenFileAsync("notes.txt", "x:=1+2;\r\n");
				message.TextDocument.Insert(0, " ");
				other.TextDocument.Insert(0, " ");

				await harness.Workspace.SaveAsync(message);
				await harness.Workspace.SaveAsync(other);

				Assert.Equal(" {100}{}{a+b,c:=d}\r\n", File.ReadAllText(message.FilePath!));
				Assert.Equal(" x:=1+2;\r\n", File.ReadAllText(other.FilePath!));
			});
		}

		[Fact]
		public void FormatOnSave_FollowsTheStyleOfTheProject()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.SetActiveProject(harness.Folder.Path, new ProjectSettings { Formatting = new FormatSettings { FormatOnSave = true, OperatorSpacing = SpacingStyle.Remove, AssignmentSpacing = SpacingStyle.Remove } });
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "x := 1 + 2;\r\n");
				document.TextDocument.Insert(0, " ");

				await harness.Workspace.SaveAsync(document);

				Assert.Equal(" x:=1+2;\r\n", File.ReadAllText(document.FilePath!));
			});
		}

		[Fact]
		public void FormatCodeOnOpen_UsesTheStyleInEffect()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings =>
				{
					settings.Editor.FormatCodeOnOpen = true;
					settings.Editor.Formatting.Reindent = true;
				});

				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Messy);

				Assert.Equal("procedure start begin\r\n   variable x := 1 + 2;\r\nend\r\n", document.TextDocument.Text);
				Assert.False(document.IsModified);
			});
		}
	}
}