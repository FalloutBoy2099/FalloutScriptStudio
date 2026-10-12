namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Shell.MainViewModelToolsTests")]
	public sealed class MainViewModelIncludeTests
	{
		[Fact]
		public void Including_a_header_inserts_an_include_line_after_the_existing_ones()
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

				Assert.StartsWith("#include \"a.h\"\r\n#include \"sub/b.h\"\r\n", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal(2, editor.LastGoTo!.Value.Line);
			});
		}

		[Fact]
		public void Headers_chosen_with_Ctrl_are_included_while_the_picker_stays_open()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String headers = harness.Folder.Combine("headers");
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ToolWindows.KeepOpenHeaders.Add(System.IO.Path.Combine(headers, "a.h"));
				harness.ToolWindows.KeepOpenHeaders.Add(System.IO.Path.Combine(headers, "b.h"));
				harness.ToolWindows.HeaderAnswer = System.IO.Path.Combine(headers, "c.h");

				await harness.Main.IncludeFileToCodeAsync();

				String text = document.TextDocument.Text;
				Assert.Contains("#include \"a.h\"", text, StringComparison.Ordinal);
				Assert.Contains("#include \"b.h\"", text, StringComparison.Ordinal);
				Assert.Contains("#include \"c.h\"", text, StringComparison.Ordinal);
				Assert.Single(harness.ToolWindows.Calls, call => call == "pick-header");
			});
		}

		[Fact]
		public void A_picker_that_was_only_used_with_Ctrl_includes_nothing_more_when_it_is_closed()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String headers = harness.Folder.Combine("headers");
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ToolWindows.KeepOpenHeaders.Add(System.IO.Path.Combine(headers, "a.h"));
				harness.ToolWindows.HeaderAnswer = null;

				await harness.Main.IncludeFileToCodeAsync();

				Assert.Single(System.Text.RegularExpressions.Regex.Matches(document.TextDocument.Text, "#include"));
			});
		}

		[Fact]
		public void Headers_chosen_with_Ctrl_are_opened_while_the_picker_stays_open()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String one = harness.Folder.Write("headers/one.h", "#define ONE 1\r\n");
				String two = harness.Folder.Write("headers/two.h", "#define TWO 2\r\n");
				harness.ToolWindows.KeepOpenHeaders.Add(one);
				harness.ToolWindows.HeaderAnswer = two;

				await harness.Main.BrowseHeadersAsync();

				Assert.Equal(2, harness.Workspace.Documents.Count);
			});
		}

		[Fact]
		public void All_includes_of_the_script_are_opened_in_the_background()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Folder.Write("scripts/one.h", "#define ONE 1\r\n");
				harness.Folder.Write("scripts/two.h", "#define TWO 2\r\n");
				DocumentViewModel document = await harness.OpenFileAsync("scripts/a.ssl", "#include \"one.h\"\r\n#include \"two.h\"\r\n#include \"missing.h\"\r\n");

				await harness.Main.OpenAllIncludesAsync();

				Assert.Equal(3, harness.Workspace.Documents.Count);
				Assert.Same(document, harness.Workspace.ActiveDocument);
			});
		}
	}
}
