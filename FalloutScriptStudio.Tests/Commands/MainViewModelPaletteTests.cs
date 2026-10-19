namespace FalloutScriptStudio.Tests.Commands
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Commands;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class MainViewModelPaletteTests
	{
		private static CommandEntry Find(ShellHarness harness, String id)
		{
			return CommandCatalog.Create(harness.Main).Single(entry => entry.Id == id);
		}

		[Fact]
		public void ThePaletteIsOpenedWithTheCatalogTheRecentCommandsAndTheShortcuts()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Recent.AddCommand("OpenCommand");
				harness.Settings.Recent.AddCommand("NewScriptCommand");
				FalloutScriptStudio.Commands.ICommandGestureSource gestures = new FakeGestures();
				harness.Main.GestureSource = gestures;

				await harness.Main.ShowCommandPaletteCommand.ExecuteAsync(null);

				FalloutScriptStudio.Services.ToolWindows.CommandPaletteRequest request = Assert.Single(harness.ToolWindows.CommandRequests);
				Assert.True(request.Entries.Count > 100);
				Assert.Equal(new[] { "OpenCommand", "NewScriptCommand" }, request.RecentIds.ToArray());
				Assert.Same(gestures, request.Gestures);
			});
		}

		[Fact]
		public void TheChosenCommandRunsThroughTheCommandOfTheMenu()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.ToolWindows.CommandAnswer = request => request.Entries.Single(entry => entry.Id == "toggle:SaveAsUtf8");
				Boolean before = harness.Main.SaveAsUtf8;

				await harness.Main.ShowCommandPaletteCommand.ExecuteAsync(null);

				Assert.Equal(!before, harness.Main.SaveAsUtf8);
			});
		}

		[Fact]
		public void ACancelledPaletteRunsNothingAndRemembersNothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.ShowCommandPaletteCommand.ExecuteAsync(null);

				Assert.Empty(harness.Settings.Recent.Commands);
			});
		}

		[Fact]
		public void AnEditorCommandGoesThroughTheActiveEditorAfterTheEditorGotTheFocusBack()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\nend\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.ToolWindows.CommandAnswer = request => request.Entries.Single(entry => entry.Id == "EditCommand:FormatDocument");

				await harness.Main.ShowCommandPaletteCommand.ExecuteAsync(null);

				Assert.Equal(new[] { "focus", "execute:FormatDocument" }, editor.Calls.Where(call => call is "focus" or "execute:FormatDocument").ToArray());
			});
		}

		[Fact]
		public void ACommandThatCannotRunIsNotRunAndNotRemembered()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				CommandEntry save = Find(harness, "SaveCommand");

				Boolean ran = await harness.Main.RunPaletteCommandAsync(save);

				Assert.False(ran);
				Assert.Empty(harness.Settings.Recent.Commands);
			});
		}

		[Fact]
		public void AnAsynchronousCommandIsAwaited()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				CommandEntry settings = Find(harness, "SettingsCommand");

				Boolean ran = await harness.Main.RunPaletteCommandAsync(settings);

				Assert.True(ran);
				Assert.Contains("settings", harness.ToolWindows.Calls);
			});
		}

		[Fact]
		public void TheLastTenCommandsAreRememberedNewestLast_AndSavedAtomicallyInTheRecentFile()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String[] ids = { "NewScriptCommand", "OpenCommand", "toggle:SaveAsUtf8", "toggle:ShowWhitespace", "toggle:AutoTrimTrailingSpaces", "toggle:FormatCodeOnOpen", "toggle:DefineToScriptsHeader", "toggle:AutoOpenMessageEditor", "toggle:MassCompileOnlyChanged", "toggle:DecompileAsFallout1", "toggle:ShowIndentLine", "SettingsCommand" };
				foreach (String id in ids)
				{
					Assert.True(await harness.Main.RunPaletteCommandAsync(Find(harness, id)), id);
				}

				Assert.Equal(RecentFiles.MaxCommands, harness.Settings.Recent.Commands.Count);
				Assert.Equal(ids.Skip(2).ToArray(), harness.Settings.Recent.Commands.ToArray());

				// A second program instance reads the same file back.
				SettingsService other = new SettingsService(harness.Paths, new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance), NullLogger<SettingsService>.Instance);
				other.Load();
				Assert.Equal(ids.Skip(2).ToArray(), other.Recent.Commands.ToArray());
				Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(harness.Paths.RecentFilesFile)!, "*.tmp"));
			});
		}

		[Fact]
		public void UsingACommandAgainMovesItToTheEnd()
		{
			RecentFiles recent = new RecentFiles();
			recent.AddCommand("a");
			recent.AddCommand("b");
			recent.AddCommand("a");

			Assert.Equal(new[] { "b", "a" }, recent.Commands.ToArray());
		}

		private sealed class FakeGestures : ICommandGestureSource
		{
			public String? GetGesture(CommandEntry entry)
			{
				return null;
			}
		}
	}
}