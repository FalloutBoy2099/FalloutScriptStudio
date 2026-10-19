namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The last commands used from the command palette, kept in the recent state file.</summary>
	public sealed class RecentCommandsTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private SettingsService CreateService()
		{
			return new SettingsService(
				AppPaths.FromBaseDirectory(this._workspace.Root),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
		}

		[Fact]
		public void TheNewestCommandIsAtTheEnd_AndUsingOneAgainMovesIt()
		{
			RecentFiles recent = new RecentFiles();
			recent.AddCommand("a");
			recent.AddCommand("b");
			recent.AddCommand("c");
			recent.AddCommand("a");

			Assert.Equal(new[] { "b", "c", "a" }, recent.Commands.ToArray());
		}

		[Fact]
		public void OnlyTheLastTenAreKept()
		{
			RecentFiles recent = new RecentFiles();
			for (Int32 i = 0; i < 25; i++)
			{
				recent.AddCommand("command" + i);
			}

			Assert.Equal(RecentFiles.MaxCommands, recent.Commands.Count);
			Assert.Equal("command15", recent.Commands[0]);
			Assert.Equal("command24", recent.Commands[^1]);
		}

		[Fact]
		public void TheIdsAreComparedIgnoringTheCase_AndAnEmptyIdIsRefused()
		{
			RecentFiles recent = new RecentFiles();
			recent.AddCommand("SaveCommand");
			recent.AddCommand("savecommand");

			Assert.Single(recent.Commands);
			Assert.Throws<ArgumentException>(() => recent.AddCommand(String.Empty));
		}

		[Fact]
		public void TheListSurvivesASaveAndALoad_AndAnOlderFileWithoutItLoadsEmpty()
		{
			SettingsService first = this.CreateService();
			first.Load();
			first.Recent.AddCommand("OpenCommand");
			first.Recent.AddCommand("EditCommand:FormatDocument");
			first.Save();

			SettingsService second = this.CreateService();
			second.Load();

			Assert.Equal(new[] { "OpenCommand", "EditCommand:FormatDocument" }, second.Recent.Commands.ToArray());
			String file = AppPaths.FromBaseDirectory(this._workspace.Root).RecentFilesFile;
			Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.tmp"));

			File.WriteAllText(file, "{\"scripts\":[\"C:\\\\a.ssl\"]}");
			SettingsService third = this.CreateService();
			third.Load();
			Assert.Empty(third.Recent.Commands);
			Assert.Single(third.Recent.Scripts);
		}
	}
}