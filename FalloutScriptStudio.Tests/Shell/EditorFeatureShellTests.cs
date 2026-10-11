namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>Opening scripts with "Auto Formatting Code", dropping files on the editor, the Alt+R / Alt+L toggles and the include highlight.</summary>
	public sealed class EditorFeatureShellTests
	{
		private const String Unformatted = "procedure start begin\r\n   x:=1+2;\r\n   y=3*4;\r\nend\r\n";

		private static String Format(String text)
		{
			return new CodeFormatter().Format(text);
		}

		[Fact]
		public void FormatCodeOnOpen_IsOffByDefault_AndTheTextIsLeftAlone()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Unformatted);

				Assert.False(harness.Main.FormatCodeOnOpen);
				Assert.Equal(Unformatted, document.TextDocument.Text);
			});
		}

		[Fact]
		public void FormatCodeOnOpen_FormatsScriptsAndHeadersWhenTheyAreOpened()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Main.FormatCodeOnOpen = true;
				Assert.NotEqual(Unformatted, Format(Unformatted));

				DocumentViewModel script = await harness.OpenFileAsync("a.ssl", Unformatted);
				DocumentViewModel header = await harness.OpenFileAsync("a.h", Unformatted);

				Assert.Equal(Format(Unformatted), script.TextDocument.Text);
				Assert.Equal(Format(Unformatted), header.TextDocument.Text);
				Assert.True(harness.Settings.Settings.Editor.FormatCodeOnOpen);
			});
		}

		[Fact]
		public void FormatCodeOnOpen_DoesNotMarkTheDocumentAsModified()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Main.FormatCodeOnOpen = true;

				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Unformatted);

				Assert.False(document.IsModified);
				Assert.False(document.TextDocument.UndoStack.CanUndo);
			});
		}

		[Fact]
		public void FormatCodeOnOpen_LeavesMessageFilesAlone()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Main.FormatCodeOnOpen = true;
				const String message = "{100}{}{a:=1+2}\r\n";

				DocumentViewModel document = await harness.OpenFileAsync("a.msg", message);

				Assert.Equal(message, document.TextDocument.Text);
			});
		}

		[Fact]
		public void FormatCodeOnOpen_AppliesToTheCopyOfAnOpenFile()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel original = await harness.OpenFileAsync("a.ssl", Unformatted);
				harness.Main.FormatCodeOnOpen = true;
				harness.Dialogs.ConfirmAnswer = true;

				await harness.Main.OpenFilesAsync(new[] { original.FilePath! });

				DocumentViewModel copy = harness.Workspace.Documents.Single(d => d != original);
				Assert.Equal(Format(Unformatted), copy.TextDocument.Text);
				Assert.Equal(Unformatted, original.TextDocument.Text);
			});
		}

		[Fact]
		public void DroppedFiles_AreAddedToTheRecentFilesAndOpened()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String path = harness.Folder.Write("dropped.ssl", Unformatted);

				IWorkspace workspace = harness.Workspace;
				await workspace.OpenDroppedFilesAsync(new[] { path });

				Assert.Contains(path, harness.Settings.Recent.Scripts, StringComparer.OrdinalIgnoreCase);
				Assert.NotNull(harness.Workspace.FindDocument(path));
			});
		}

		[Fact]
		public void DroppedFile_ThatIsOpenAlready_IsOfferedAsACopy()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel original = await harness.OpenFileAsync("a.ssl", Unformatted);
				harness.Dialogs.ConfirmAnswer = true;

				IWorkspace workspace = harness.Workspace;
				await workspace.OpenDroppedFilesAsync(new[] { original.FilePath! });

				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.Single(harness.Dialogs.Confirmations);
				Assert.Null(harness.Workspace.Documents.Single(d => d != original).FilePath);
			});
		}

		[Fact]
		public void DroppedFile_ThatIsOpenAlready_ActivatesItWhenNoCopyIsWanted()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel original = await harness.OpenFileAsync("a.ssl", Unformatted);
				await harness.OpenFileAsync("b.ssl", Unformatted);
				harness.Dialogs.ConfirmAnswer = false;

				IWorkspace workspace = harness.Workspace;
				await workspace.OpenDroppedFilesAsync(new[] { original.FilePath! });

				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.Same(original, harness.Workspace.ActiveDocument);
			});
		}

		[Fact]
		public void DroppedFiles_OfTheWindowTakeTheSamePathAsThoseOfTheEditor()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel original = await harness.OpenFileAsync("a.ssl", Unformatted);
				harness.Dialogs.ConfirmAnswer = true;

				await harness.Main.OpenFilesAsync(new[] { original.FilePath! });

				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.Single(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void ToggleRightBrowser_FlipsTheViewOption()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				Boolean before = harness.Main.ShowRightBrowser;

				harness.Main.ToggleRightBrowserCommand.Execute(null);
				Assert.Equal(!before, harness.Main.ShowRightBrowser);

				harness.Main.ToggleRightBrowserCommand.Execute(null);
				Assert.Equal(before, harness.Main.ShowRightBrowser);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void ToggleLog_FlipsTheLogWindowSetting()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				Boolean before = harness.Main.ShowLog;

				harness.Main.ToggleLogCommand.Execute(null);
				Assert.Equal(!before, harness.Main.ShowLog);
				Assert.Equal(!before, harness.Settings.Settings.Window.ShowLog);

				harness.Main.ToggleLogCommand.Execute(null);
				Assert.Equal(before, harness.Main.ShowLog);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void IncludingAHeader_HighlightsTheInsertedLine()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String headers = harness.Folder.Combine("headers");
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "#include \"a.h\"\r\n\r\nprocedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ToolWindows.HeaderAnswer = System.IO.Path.Combine(headers, "sub", "b.h");

				await harness.Main.IncludeFileToCodeAsync();

				Assert.NotNull(editor.InsertedHighlight);
				Int32 line = document.TextDocument.GetLocation(editor.InsertedHighlight.Value.Offset).Line;
				Assert.Equal(2, line);
				Assert.Equal("#include \"sub/b.h\"", document.TextDocument.GetText(editor.InsertedHighlight.Value.Offset, editor.InsertedHighlight.Value.Length));
			});
		}
	}
}
