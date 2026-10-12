namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Startup;
	using Xunit;

	public sealed class StartupCoordinatorTests
	{
		private static readonly String[] _mainEditorSequence =
		{
			"single.TryAcquire",
			"services.Create",
			"folders.EnsureCreated",
			"folders.PrepareScratch",
			"services.LoadSettings",
			"services.ActivateProject",
			"services.LoadStores",
			"services.ApplyTheme",
			"services.GetStoredLanguage",
			"host.InitializeLanguage",
			"host.RunMainEditor",
		};

		[Fact]
		public async Task MainEditor_RunsTheStepsInOrder()
		{
			StartupHarness harness = new StartupHarness(true);

			await harness.RunAsync("script.ssl");

			Assert.Equal(_mainEditorSequence, harness.Events.Items);
			Assert.Equal(new[] { "script.ssl" }, harness.Host.MainEditorArguments);
			Assert.Empty(harness.Host.ShutdownCodes);
			Assert.Contains("Run main editor...", harness.Log);
		}

		[Fact]
		public async Task MainEditor_KeepsTheLockAndOwnsTheScratchFolder()
		{
			StartupHarness harness = new StartupHarness(true);

			await harness.RunAsync();

			Assert.Same(harness.SingleInstance, harness.Session.SingleInstance);
			Assert.Same(harness.Services, harness.Session.Services);
			Assert.True(harness.Session.OwnsScratch);
			Assert.True(harness.Session.SettingsLoaded);
			Assert.Equal(0, harness.SingleInstance.DisposeCount);
		}

		[Fact]
		public async Task Language_IsTheStoredOne()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.StoredLanguage = "fr";

			await harness.RunAsync();

			Assert.Equal("fr", harness.Host.Language);
		}

		[Fact]
		public async Task Language_IsNullWhenNoneIsStored()
		{
			StartupHarness harness = new StartupHarness(true);

			await harness.RunAsync();

			Assert.Null(harness.Host.Language);
		}

		[Fact]
		public async Task MainEditor_IsOpenedOnlyAfterTheSettingsAreLoaded()
		{
			StartupHarness harness = new StartupHarness(true);
			Boolean loadedWhenOpened = false;
			harness.Host.OnRunMainEditor = () => loadedWhenOpened = harness.Session.SettingsLoaded;

			await harness.RunAsync();

			// The first run (the settings dialog, then IsFirstRunCompleted) starts when the main window is loaded: never before the settings are read.
			Assert.True(loadedWhenOpened);
			Assert.True(harness.Events.Items.ToList().IndexOf("services.LoadSettings") < harness.Events.Items.ToList().IndexOf("host.RunMainEditor"));
		}

		[Fact]
		public async Task StandaloneMessageEditor_DoesNotPrepareTheScratchFolder()
		{
			StartupHarness harness = new StartupHarness(true);

			await harness.RunAsync("C:\\mods\\dialog.msg");

			Assert.Equal(1, harness.Events.Count("folders.EnsureCreated"));
			Assert.Equal(0, harness.Events.Count("folders.PrepareScratch"));
			Assert.False(harness.Session.OwnsScratch);
			Assert.True(harness.Session.SettingsLoaded);
			Assert.Equal("C:\\mods\\dialog.msg", harness.Host.MessagePath);
			Assert.Null(harness.Host.MainEditorArguments);
		}

		[Fact]
		public async Task StandaloneMessageEditor_ReleasesTheLockAfterTheSettingsAreLoaded()
		{
			StartupHarness harness = new StartupHarness(true);

			await harness.RunAsync("dialog.msg");

			Assert.Equal(1, harness.SingleInstance.DisposeCount);
			Assert.Null(harness.Session.SingleInstance);
			Assert.False(harness.SingleInstance.Listening);
			Assert.Equal(new[] { "services.ApplyTheme", "services.GetStoredLanguage", "host.InitializeLanguage", "single.Dispose", "host.RunStandaloneMessageEditor" }, harness.Events.Items.Skip(6));
			Assert.Contains("Run only message editor...", harness.Log);
		}

		[Fact]
		public async Task SecondInstance_WithArguments_ForwardsThemAndEndsWithoutAnyService()
		{
			StartupHarness harness = new StartupHarness(false);

			await harness.RunAsync("script.ssl");

			Assert.Equal(new[] { "single.TryAcquire", "single.Send", "single.Dispose", "host.Shutdown" }, harness.Events.Items);
			Assert.Equal(new[] { "script.ssl" }, harness.SingleInstance.Sent);
			Assert.Equal(new[] { 0 }, harness.Host.ShutdownCodes);
			Assert.Empty(harness.Host.Information);
			Assert.Null(harness.Session.Services);
			Assert.False(harness.Session.SettingsLoaded);
		}

		[Fact]
		public async Task SecondInstance_WithoutArguments_ShowsTheAlreadyRunningNotice()
		{
			StartupHarness harness = new StartupHarness(false);

			await harness.RunAsync();

			Assert.Equal(new[] { "single.TryAcquire", "host.ShowInformation", "single.Dispose", "host.Shutdown" }, harness.Events.Items);
			Assert.Equal(new[] { Loc.Get("Str.Shell.App.AlreadyRunning") }, harness.Host.Information);
			Assert.NotEqual("Str.Shell.App.AlreadyRunning", harness.Host.Information[0]);
			Assert.Equal(new[] { 0 }, harness.Host.ShutdownCodes);
			Assert.Null(harness.SingleInstance.Sent);
		}

		[Fact]
		public async Task SecondInstance_WhenTheRunningOneDoesNotAnswer_TakesOverTheStart()
		{
			StartupHarness harness = new StartupHarness(false, true);
			harness.SingleInstance.SendResult = false;

			await harness.RunAsync("script.ssl");

			// The running instance has probably ended meanwhile: the lock is taken again and this process becomes the main editor.
			Assert.Equal(new[] { "single.TryAcquire", "single.Send", "single.TryAcquire", "services.Create" }, harness.Events.Items.Take(4));
			Assert.Contains("The running instance did not take the command line.", harness.Log);
			Assert.Equal("host.RunMainEditor", harness.Events.Items[^1]);
			Assert.Equal(0, harness.SingleInstance.DisposeCount);
			Assert.True(harness.Session.OwnsScratch);
		}

		[Fact]
		public async Task SecondInstance_WhenTheRunningOneDoesNotAnswerAndStillOwnsTheLock_ShowsTheNotice()
		{
			StartupHarness harness = new StartupHarness(false, false);
			harness.SingleInstance.SendResult = false;

			await harness.RunAsync("script.ssl");

			Assert.Equal(
				new[] { "single.TryAcquire", "single.Send", "single.TryAcquire", "host.ShowInformation", "single.Dispose", "host.Shutdown" },
				harness.Events.Items);
			Assert.Equal(new[] { 0 }, harness.Host.ShutdownCodes);
		}

		[Fact]
		public async Task SecondInstance_WithAMessageFile_ForwardsItInsteadOfRunningTheEditor()
		{
			StartupHarness harness = new StartupHarness(false);

			await harness.RunAsync("dialog.msg");

			Assert.Equal(new[] { "dialog.msg" }, harness.SingleInstance.Sent);
			Assert.Null(harness.Host.MessagePath);
		}

		[Fact]
		public async Task FolderProblems_AreLoggedAndDoNotStopTheStart()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Folders.EnsureCreatedProblems = new[] { "no settings folder" };
			harness.Folders.PrepareScratchProblems = new[] { "locked.tmp" };

			await harness.RunAsync();

			Assert.Contains("Preparing the folders: no settings folder", harness.Log);
			Assert.Contains("Preparing the scratch folder: locked.tmp", harness.Log);
			Assert.True(harness.Session.SettingsLoaded);
			Assert.Equal("host.RunMainEditor", harness.Events.Items[^1]);
		}

		[Fact]
		public async Task Failure_WhileLoadingTheSettings_IsLoggedShownAndEndsWithExitCodeOne()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.LoadSettingsFailure = new InvalidOperationException("boom");

			await harness.RunAsync();

			Assert.Equal(new[] { Loc.Format("Str.Shell.App.StartFailed", "boom") }, harness.Host.Errors);
			Assert.Contains("boom", harness.Host.Errors[0]);
			Assert.Equal(new[] { 1 }, harness.Host.ShutdownCodes);
			Assert.Contains(harness.Log, line => line.StartsWith("Start-up failed: ", StringComparison.Ordinal) && line.Contains("boom", StringComparison.Ordinal));
			Assert.False(harness.Session.SettingsLoaded);
			Assert.Equal(0, harness.Events.Count("host.RunMainEditor"));
		}

		[Fact]
		public async Task Failure_AfterTheSettingsWereLoaded_KeepsTheSettingsLoadedFlag()
		{
			StartupHarness harness = new StartupHarness(true);
			harness.Services.LoadStoresFailure = new InvalidOperationException("stores");

			await harness.RunAsync();

			Assert.True(harness.Session.SettingsLoaded);
			Assert.Equal(new[] { 1 }, harness.Host.ShutdownCodes);
			Assert.Equal(0, harness.Events.Count("services.ApplyTheme"));
		}

		[Fact]
		public async Task Failure_WhileCreatingTheServices_DoesNotTouchTheFolders()
		{
			StartupHarness failing = new StartupHarness(true);
			Boolean created = false;
			StartupCoordinator coordinator = new StartupCoordinator(
				failing.Host,
				failing.Folders,
				() => failing.SingleInstance,
				_ =>
				{
					created = true;
					throw new InvalidOperationException("no container");
				},
				failing.Session,
				line => failing.Log.Add(line));

			await coordinator.RunAsync(Array.Empty<String>());

			Assert.True(created);
			Assert.Equal(0, failing.Events.Count("folders.EnsureCreated"));
			Assert.Equal(new[] { 1 }, failing.Host.ShutdownCodes);
			Assert.False(failing.Session.SettingsLoaded);
		}

		[Fact]
		public async Task RunAsync_RejectsNullArguments()
		{
			StartupHarness harness = new StartupHarness(true);

			await Assert.ThrowsAsync<ArgumentNullException>(() => harness.Coordinator.RunAsync(null!));
		}
	}
}
