namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>A start-up that fails, a file that cannot be read for a moment or a second instance must never cost the user his settings.</summary>
	public sealed class SettingsServiceSafetyTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private AppPaths Paths => AppPaths.FromBaseDirectory(this._workspace.Root);

		private SettingsService CreateService(Func<String, Byte[]>? readFile = null)
		{
			return new SettingsService(
				this.Paths,
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance)
			{
				ReadRetryDelay = TimeSpan.Zero,
				ReadFile = readFile ?? File.ReadAllBytes,
			};
		}

		private SettingsService CreateSavedService()
		{
			SettingsService first = this.CreateService();
			first.Settings.Editor.TabSize = 7;
			first.Settings.Window.SetPlacement(WindowKind.Main, new WindowPlacement { X = 1, Y = 2, Width = 300, Height = 400 });
			first.Recent.AddScript(@"C:\a.ssl");
			first.Save();
			return first;
		}

		[Fact]
		public void SaveWithoutLoad_DoesNotWriteTheDefaults()
		{
			// A start-up that failed before Load: the exit Save must not create or replace any file.
			SettingsService blank = this.CreateService();

			blank.Save();

			Assert.False(File.Exists(this.Paths.SettingsFile));
			Assert.False(File.Exists(this.Paths.RecentFilesFile));
		}

		[Fact]
		public void SaveWithoutLoad_KeepsTheExistingFiles()
		{
			this.CreateSavedService();
			Byte[] before = File.ReadAllBytes(this.Paths.SettingsFile);

			this.CreateService().Save();

			Assert.Equal(before, File.ReadAllBytes(this.Paths.SettingsFile));
			Assert.Contains("\"tabSize\": 7", File.ReadAllText(this.Paths.SettingsFile), StringComparison.Ordinal);
		}

		[Fact]
		public async Task SaveAsyncWithoutLoad_KeepsTheExistingFiles()
		{
			this.CreateSavedService();
			Byte[] before = File.ReadAllBytes(this.Paths.RecentFilesFile);

			await this.CreateService().SaveAsync();

			Assert.Equal(before, File.ReadAllBytes(this.Paths.RecentFilesFile));
		}

		[Fact]
		public void UnreadableFile_IsNotTakenForAbsentAndNotOverwritten()
		{
			this.CreateSavedService();
			Byte[] before = File.ReadAllBytes(this.Paths.SettingsFile);
			SettingsService service = this.CreateService();

			using (new FileStream(this.Paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				service.Load();
			}

			// The defaults are in memory, but the real file is not replaced by them.
			Assert.Equal(EditorSettings.DefaultTabSize, service.Settings.Editor.TabSize);
			service.Save();
			Assert.Equal(before, File.ReadAllBytes(this.Paths.SettingsFile));
		}

		[Fact]
		public void UnreadableFile_DoesNotImportTheLegacyData()
		{
			this.CreateSavedService();
			SettingsService service = this.CreateService(path => path == this.Paths.SettingsFile ? throw new IOException("locked") : File.ReadAllBytes(path));

			service.Load();

			Assert.False(service.ImportedLegacyData);
		}

		[Fact]
		public void UnreadableFile_IsMergedOnceItCanBeReadAgain()
		{
			this.CreateSavedService();
			Boolean locked = true;
			SettingsService service = this.CreateService(path => locked && path == this.Paths.SettingsFile ? throw new IOException("locked") : File.ReadAllBytes(path));
			service.Load();
			service.Settings.Editor.FontSizeAdjustment = 3;
			locked = false;

			service.Save();

			SettingsService reloaded = this.CreateService();
			reloaded.Load();
			Assert.Equal(3, reloaded.Settings.Editor.FontSizeAdjustment);
			Assert.Equal(7, reloaded.Settings.Editor.TabSize);
		}

		[Fact]
		public void TransientReadFailure_IsRetried()
		{
			this.CreateSavedService();
			Int32 failures = 2;
			SettingsService service = this.CreateService(path =>
			{
				if (path == this.Paths.SettingsFile && failures-- > 0)
				{
					throw new IOException("sharing violation");
				}

				return File.ReadAllBytes(path);
			});

			service.Load();

			Assert.Equal(7, service.Settings.Editor.TabSize);
		}

		[Fact]
		public void SaveAfterLoad_DoesNotTouchUnchangedFiles()
		{
			this.CreateSavedService();
			SettingsService service = this.CreateService();
			service.Load();
			File.WriteAllText(this.Paths.RecentFilesFile, "{ \"scripts\": [ \"D:\\\\other.ssl\" ], \"messages\": [] }");

			service.Save();

			Assert.Contains("other.ssl", File.ReadAllText(this.Paths.RecentFilesFile), StringComparison.Ordinal);
		}

		[Fact]
		public void Save_MergesWithTheChangesOfAnotherInstance()
		{
			this.CreateSavedService();
			SettingsService editor = this.CreateService();
			editor.Load();
			SettingsService messageEditor = this.CreateService();
			messageEditor.Load();

			// The stand-alone message editor saves its window and a recent file while the main editor is still open ...
			messageEditor.Settings.Window.SetPlacement(WindowKind.MessageEditor, new WindowPlacement { X = 10, Y = 20, Width = 500, Height = 600 });
			messageEditor.Recent.AddMessage(@"C:\a.msg");
			messageEditor.Save();

			// ... and the main editor, whose snapshot is older, saves what it changed.
			editor.Settings.Editor.TabSize = 5;
			editor.Save();

			SettingsService reloaded = this.CreateService();
			reloaded.Load();
			Assert.Equal(5, reloaded.Settings.Editor.TabSize);
			Assert.Equal(500, reloaded.Settings.Window.GetPlacement(WindowKind.MessageEditor)!.Width);
			Assert.Equal(300, reloaded.Settings.Window.GetPlacement(WindowKind.Main)!.Width);
			Assert.Equal(new[] { @"C:\a.msg" }, reloaded.Recent.Messages);
			Assert.Equal(new[] { @"C:\a.ssl" }, reloaded.Recent.Scripts);
		}

		[Fact]
		public async Task SaveAsync_MergesWithTheChangesOfAnotherInstance()
		{
			this.CreateSavedService();
			SettingsService editor = this.CreateService();
			editor.Load();
			SettingsService other = this.CreateService();
			other.Load();
			other.Recent.AddMessage(@"C:\a.msg");
			await other.SaveAsync();

			editor.Settings.Editor.TabSize = 5;
			await editor.SaveAsync();

			SettingsService reloaded = this.CreateService();
			reloaded.Load();
			Assert.Equal(5, reloaded.Settings.Editor.TabSize);
			Assert.Equal(new[] { @"C:\a.msg" }, reloaded.Recent.Messages);
		}

		[Fact]
		public void Load_DeletesOldOrphanedTemporaryFilesOnly()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			String old = Path.Combine(this.Paths.SettingsDirectory, "settings.json." + Guid.NewGuid().ToString("N") + ".tmp");
			String recent = Path.Combine(this.Paths.SettingsDirectory, "recent.json." + Guid.NewGuid().ToString("N") + ".tmp");
			String unrelated = Path.Combine(this.Paths.SettingsDirectory, "notes.tmp");
			File.WriteAllText(old, "x");
			File.WriteAllText(recent, "x");
			File.WriteAllText(unrelated, "x");
			File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));
			File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddHours(-2));

			this.CreateService().Load();

			Assert.False(File.Exists(old));
			Assert.True(File.Exists(recent));
			Assert.True(File.Exists(unrelated));
		}

		[Fact]
		public void Save_LeavesNoTemporaryFile()
		{
			this.CreateSavedService();

			Assert.Empty(Directory.GetFiles(this.Paths.SettingsDirectory, "*.tmp"));
			Assert.Equal(2, Directory.GetFiles(this.Paths.SettingsDirectory, "*.json").Length);
		}
	}
}