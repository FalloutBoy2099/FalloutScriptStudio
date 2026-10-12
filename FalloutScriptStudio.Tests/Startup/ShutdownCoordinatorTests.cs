namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Startup;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class ShutdownCoordinatorTests
	{
		[Fact]
		public async Task Exit_SavesTheSettingsAndDeletesTheScratchFolderOfAMainEditor()
		{
			StartupHarness harness = new StartupHarness(true);
			await harness.RunAsync();

			harness.Shutdown.Run();

			Assert.Equal(1, harness.Events.Count("services.SaveSettings"));
			Assert.Equal(1, harness.Events.Count("folders.DeleteScratch"));
			Assert.True(harness.Events.Items.ToList().IndexOf("services.SaveSettings") < harness.Events.Items.ToList().IndexOf("folders.DeleteScratch"));
		}

		[Fact]
		public async Task Exit_ReleasesTheLockAndTheServices()
		{
			StartupHarness harness = new StartupHarness(true);
			await harness.RunAsync();

			harness.Shutdown.Run();

			Assert.Equal(1, harness.SingleInstance.DisposeCount);
			Assert.Equal(1, harness.Services.DisposeCount);
			Assert.Null(harness.Session.SingleInstance);
			Assert.Null(harness.Session.Services);
		}

		[Fact]
		public async Task Exit_AfterTheStandaloneMessageEditor_SavesTheSettingsButLeavesTheScratchFolder()
		{
			StartupHarness harness = new StartupHarness(true);
			await harness.RunAsync("dialog.msg");

			harness.Shutdown.Run();

			Assert.Equal(1, harness.Events.Count("services.SaveSettings"));
			Assert.Equal(0, harness.Events.Count("folders.DeleteScratch"));
		}

		[Fact]
		public async Task Exit_DoesNotSaveTheSettingsThatWereNeverLoaded()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.LoadSettingsFailure = new InvalidOperationException("boom");
			await harness.RunAsync();

			harness.Shutdown.Run();

			Assert.Equal(0, harness.Events.Count("services.SaveSettings"));
			Assert.Contains("The settings were not loaded; the settings files are left as they are.", harness.Log);
			Assert.Equal(1, harness.Services.DisposeCount);
		}

		[Fact]
		public void Exit_BeforeAnythingStarted_DoesNothing()
		{
			StartupHarness harness = new StartupHarness(true);

			harness.Shutdown.Run();

			Assert.Empty(harness.Events.Items);
			Assert.Contains("The settings were not loaded; the settings files are left as they are.", harness.Log);
		}

		[Fact]
		public async Task Exit_AfterAFailureWhileOpeningTheMainEditor_StillSavesTheLoadedSettings()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.LoadStoresFailure = new InvalidOperationException("stores");
			await harness.RunAsync();

			harness.Shutdown.Run();

			Assert.Equal(1, harness.Events.Count("services.SaveSettings"));
			Assert.Equal(1, harness.Events.Count("folders.DeleteScratch"));
		}

		[Theory]
		[InlineData(typeof(IOException))]
		[InlineData(typeof(UnauthorizedAccessException))]
		public async Task Exit_WhenTheSaveFails_LogsAndGoesOn(Type failure)
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.SaveFailure = (Exception)Activator.CreateInstance(failure, "disk full")!;
			await harness.RunAsync();

			harness.Shutdown.Run();

			Assert.Contains("Saving the settings failed: disk full", harness.Log);
			Assert.Equal(1, harness.Events.Count("folders.DeleteScratch"));
			Assert.Equal(1, harness.Services.DisposeCount);
		}

		[Fact]
		public async Task Exit_DoesNotSwallowOtherSaveFailures()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.SaveFailure = new InvalidOperationException("bug");
			await harness.RunAsync();

			Assert.Throws<InvalidOperationException>(() => harness.Shutdown.Run());
		}

		[Fact]
		public async Task Exit_LogsTheScratchFolderProblems()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Folders.DeleteScratchProblems = new[] { "locked.tmp" };
			await harness.RunAsync();

			harness.Shutdown.Run();

			Assert.Contains("Deleting the scratch folder: locked.tmp", harness.Log);
		}

		[Fact]
		public async Task Exit_RunTwice_ReleasesOnce()
		{
			StartupHarness harness = new StartupHarness(true);
			await harness.RunAsync();

			harness.Shutdown.Run();
			harness.Shutdown.Run();

			Assert.Equal(1, harness.SingleInstance.DisposeCount);
			Assert.Equal(1, harness.Services.DisposeCount);
		}

		[Fact]
		public async Task RealFolders_ScratchFolderOfTheMainEditorIsPurgedAtStartAndDeletedAtExit()
		{
			using TestFolder folder = new TestFolder("startup-main");
			AppPaths paths = AppPaths.FromBaseDirectory(folder.Path);
			Directory.CreateDirectory(paths.ScratchDirectory);
			String leftover = Path.Combine(paths.ScratchDirectory, "left-over.tmp");
			File.WriteAllText(leftover, "old");
			StartupHarness harness = new StartupHarness(true);
			AppPathsFolders folders = new AppPathsFolders(paths);
			StartupCoordinator coordinator = new StartupCoordinator(harness.Host, folders, () => harness.SingleInstance, _ => harness.Services, harness.Session, line => harness.Log.Add(line));
			ShutdownCoordinator shutdown = new ShutdownCoordinator(harness.Session, folders, line => harness.Log.Add(line));

			await coordinator.RunAsync(Array.Empty<String>());
			Assert.True(Directory.Exists(paths.ScratchDirectory));
			Assert.False(File.Exists(leftover));
			Assert.True(Directory.Exists(paths.SettingsDirectory));

			shutdown.Run();
			Assert.False(Directory.Exists(paths.ScratchDirectory));
		}

		[Fact]
		public async Task RealFolders_ScratchFolderOfAMainEditorSurvivesTheStandaloneMessageEditor()
		{
			using TestFolder folder = new TestFolder("startup-standalone");
			AppPaths paths = AppPaths.FromBaseDirectory(folder.Path);
			Directory.CreateDirectory(paths.ScratchDirectory);
			String inUse = Path.Combine(paths.ScratchDirectory, "in-use-by-the-main-editor.tmp");
			File.WriteAllText(inUse, "live");
			StartupHarness harness = new StartupHarness(true);
			AppPathsFolders folders = new AppPathsFolders(paths);
			StartupCoordinator coordinator = new StartupCoordinator(harness.Host, folders, () => harness.SingleInstance, _ => harness.Services, harness.Session, line => harness.Log.Add(line));
			ShutdownCoordinator shutdown = new ShutdownCoordinator(harness.Session, folders, line => harness.Log.Add(line));

			await coordinator.RunAsync(new[] { Path.Combine(folder.Path, "dialog.msg") });
			shutdown.Run();

			Assert.True(File.Exists(inUse));
		}
	}
}
