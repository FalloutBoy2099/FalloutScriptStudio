namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>Unchecking "Search includes" must stop every lookup in the headers folder, not only the compiler command line.</summary>
	public sealed class IncludeSearchWorkspaceTests
	{
		private const String SharedHeader = "#define SHARED 1\r\n";

		private static (String Headers, String ScriptsHeader) ConfigureHeaders(ShellHarness harness, Boolean search, Boolean withScriptsHeader = true)
		{
			String shared = harness.Folder.Write("headers/shared.h", SharedHeader);
			String headers = Path.GetDirectoryName(shared)!;
			String scriptsHeader = Path.Combine(headers, "scripts.h");
			harness.Settings.Update(settings =>
			{
				settings.Paths.HeadersDirectory = headers;
				settings.Paths.ScriptsHeaderFile = withScriptsHeader ? scriptsHeader : null;
				settings.Compiler.SearchIncludePath = search;
			});
			return (headers, scriptsHeader);
		}

		[Fact]
		public void ParserOptions_ReceiveTheHeadersFolderOnlyWhileTheSearchIsEnabled()
		{
			using ShellHarness harness = new ShellHarness();
			(String headers, _) = ConfigureHeaders(harness, true);

			ParserOptions enabled = ParserOptionsFactory.Create(harness.Settings);
			Assert.Equal(headers, enabled.HeadersDirectory);
			Assert.Equal(headers, enabled.EffectiveGlobalMacroDirectory);

			harness.Settings.Update(settings => settings.Compiler.SearchIncludePath = false);
			ParserOptions disabled = ParserOptionsFactory.Create(harness.Settings);
			Assert.Null(disabled.HeadersDirectory);
			Assert.Equal(Path.Combine(harness.Folder.Path, "scripts", "shared.h"), disabled.CreateIncludeResolver().Resolve("shared.h", Path.Combine(harness.Folder.Path, "scripts")));
			Assert.Equal(headers, disabled.EffectiveGlobalMacroDirectory);
		}

		[Fact]
		public void ParserOptions_NeedAScriptsHeaderFileToo()
		{
			using ShellHarness harness = new ShellHarness();
			ConfigureHeaders(harness, true, withScriptsHeader: false);

			Assert.Null(ParserOptionsFactory.Create(harness.Settings).HeadersDirectory);
		}

		[Fact]
		public void OpenInclude_FindsAHeaderInTheHeadersFolderOnlyWhileTheSearchIsEnabled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(String headers, _) = ConfigureHeaders(harness, true);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/a.ssl", "#include \"shared.h\"\r\n");

				await harness.Workspace.OpenIncludeAsync(script, "shared.h");
				Assert.Contains(harness.Workspace.Documents, document => String.Equals(document.FilePath, Path.Combine(headers, "shared.h"), StringComparison.OrdinalIgnoreCase));
				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void OpenInclude_DoesNotLookInTheHeadersFolderWhenTheSearchIsDisabled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				ConfigureHeaders(harness, false);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/a.ssl", "#include \"shared.h\"\r\n");

				await harness.Workspace.OpenIncludeAsync(script, "shared.h");

				Assert.Single(harness.Workspace.Documents);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void OpenInclude_OfAnUnsavedScriptIsRefusedWhenNoHeadersFolderIsSearched()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				ConfigureHeaders(harness, false);
				DocumentViewModel script = harness.Workspace.CreateNew("#include \"shared.h\"\r\n");

				await harness.Workspace.OpenIncludeAsync(script, "shared.h");

				Assert.Single(harness.Dialogs.Messages);
				Assert.Single(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void OpenAllIncludes_SkipsTheHeadersFolderWhenTheSearchIsDisabled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				ConfigureHeaders(harness, false);
				harness.Folder.Write("scripts/local.h", "#define LOCAL 1\r\n");
				await harness.OpenFileAsync("scripts/a.ssl", "#include \"local.h\"\r\n#include \"shared.h\"\r\n");

				await harness.Main.OpenAllIncludesAsync();

				Assert.Equal(new[] { "a.ssl", "local.h" }, harness.Workspace.Documents.Select(document => Path.GetFileName(document.FilePath!)).OrderBy(name => name, StringComparer.Ordinal).ToArray());
			});
		}

		[Fact]
		public void OpenAllIncludes_UsesTheHeadersFolderWhenTheSearchIsEnabled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				ConfigureHeaders(harness, true);
				await harness.OpenFileAsync("scripts/a.ssl", "#include \"shared.h\"\r\n");

				await harness.Main.OpenAllIncludesAsync();

				Assert.Contains(harness.Workspace.Documents, document => String.Equals(Path.GetFileName(document.FilePath), "shared.h", StringComparison.OrdinalIgnoreCase));
			});
		}

		[Fact]
		public void TheEditorIncludeResolver_FollowsTheSameRule()
		{
			using EditorTestEnvironment environment = new EditorTestEnvironment();
			String headers = Path.Combine(environment.SettingsService.Paths.ProgramDirectory, "headers");
			Directory.CreateDirectory(headers);
			File.WriteAllText(Path.Combine(headers, "shared.h"), SharedHeader);
			String scripts = Path.Combine(environment.SettingsService.Paths.ProgramDirectory, "scripts");
			environment.SettingsService.Settings.Paths.HeadersDirectory = headers;
			environment.SettingsService.Settings.Paths.ScriptsHeaderFile = Path.Combine(headers, "scripts.h");

			environment.SettingsService.Settings.Compiler.SearchIncludePath = true;
			Assert.Equal(Path.Combine(headers, "shared.h"), environment.Services.CreateIncludeResolver().Resolve("shared.h", scripts));

			environment.SettingsService.Settings.Compiler.SearchIncludePath = false;
			Assert.Equal(Path.Combine(scripts, "shared.h"), environment.Services.CreateIncludeResolver().Resolve("shared.h", scripts));

			environment.SettingsService.Settings.Compiler.SearchIncludePath = true;
			environment.SettingsService.Settings.Paths.ScriptsHeaderFile = null;
			Assert.Equal(Path.Combine(scripts, "shared.h"), environment.Services.CreateIncludeResolver().Resolve("shared.h", scripts));
		}
	}
}
