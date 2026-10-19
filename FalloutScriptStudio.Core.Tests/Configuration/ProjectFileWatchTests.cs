namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>
	/// The project file is watched: a change made by hand is reloaded, an invalid or half-written content never replaces the last good
	/// settings, the editor's own saves are ignored, and the watcher ends with the project. These tests use the real file system under
	/// <c>_test_files\work</c> with short delays.
	/// </summary>
	public sealed class ProjectFileWatchTests : IDisposable
	{
		private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
		private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

		private static readonly ProjectFileWatchOptions Fast = new ProjectFileWatchOptions
		{
			Debounce = TimeSpan.FromMilliseconds(40),
			PollInterval = TimeSpan.FromMilliseconds(150),
			RetryDelay = TimeSpan.FromMilliseconds(20),
			ReadAttempts = 8,
			SettleAttempts = 3,
		};

		/// <summary>
		/// Like <see cref="Fast"/> but a file that is missing is looked at again for about five seconds before it counts as deleted: a test that deletes a file and
		/// writes it again must not depend on how long the machine takes between the two, which is what a loaded machine changes.
		/// </summary>
		private static readonly ProjectFileWatchOptions Patient = new ProjectFileWatchOptions
		{
			Debounce = Fast.Debounce,
			PollInterval = Fast.PollInterval,
			RetryDelay = Fast.RetryDelay,
			ReadAttempts = Fast.ReadAttempts,
			SettleAttempts = 8,
		};

		private readonly TestFolder _root = new TestFolder("project-watch");
		private readonly SettingsService _settings;
		private readonly List<IDisposable> _disposables = [];

		public ProjectFileWatchTests()
		{
			this._settings = new SettingsService(
				AppPaths.FromBaseDirectory(this._root.Combine("app")),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			this._settings.Load();
		}

		public void Dispose()
		{
			foreach (IDisposable disposable in this._disposables)
			{
				disposable.Dispose();
			}

			this._settings.Dispose();
			this._root.Dispose();
		}

		private static void Edit(String path, String content)
		{
			File.WriteAllText(path, content);
		}

		private String MakeProject(String json = "{ \"name\": \"First\" }")
		{
			String folder = this._root.Combine("mod");
			Directory.CreateDirectory(folder);
			Edit(Path.Combine(folder, ProjectSettings.FileName), json);
			return folder;
		}

		private (ProjectService Service, Recorder Events) Open(String folder, ProjectFileWatchOptions? options = null)
		{
			// The service must not post to the synchronization context of the test runner.
			SynchronizationContext? previous = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext(null);
			ProjectService service;
			try
			{
				service = new ProjectService(this._settings, NullLogger<ProjectService>.Instance, options ?? Fast);
			}
			finally
			{
				SynchronizationContext.SetSynchronizationContext(previous);
			}

			this._disposables.Add(service);
			Recorder recorder = new Recorder(service);
			Assert.True(service.Open(folder));
			recorder.Reset();
			return (service, recorder);
		}

		[Fact]
		public async Task AValidHandEdit_IsReloaded_AndTakesEffect()
		{
			String folder = this.MakeProject("{ \"name\": \"First\", \"optimization\": \"none\" }");
			(ProjectService service, Recorder events) = this.Open(folder);

			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Second\", \"optimization\": \"full\", \"defines\": [ \"EDITED\" ] }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal(Path.Combine(folder, ProjectSettings.FileName), change.Path);
			Assert.Null(change.Error);
			Assert.Equal("Second", service.DisplayName);
			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.Contains("EDITED", this._settings.Effective.DefineChoices);
			Assert.Equal(1, events.ProjectChanges);
		}

		[Fact]
		public async Task TheEditorsOwnSave_DoesNotReload()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder);

			ProjectSettings edited = service.Settings!.Clone();
			edited.Name = "Saved by the editor";
			edited.Optimization = OptimizationLevel.Experimental;
			Assert.True(service.SaveSettings(edited));
			Assert.Equal(1, events.ProjectChanges);

			await events.ExpectNoFileChangeAsync();

			Assert.Equal(1, events.ProjectChanges);
			Assert.Equal("Saved by the editor", service.DisplayName);
		}

		[Fact]
		public async Task TheEditorsOwnSave_FollowedByAHandEdit_ReloadsOnlyTheHandEdit()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder);
			ProjectSettings edited = service.Settings!.Clone();
			edited.Name = "Saved";
			service.SaveSettings(edited);
			await events.ExpectNoFileChangeAsync();

			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"By hand\" }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("By hand", service.DisplayName);
		}

		[Fact]
		public async Task ACreatedFileInAFolderWithoutSettings_IsPickedUp()
		{
			String folder = this.MakeProject();
			File.Delete(Path.Combine(folder, ProjectSettings.FileName));
			(ProjectService service, Recorder events) = this.Open(folder);
			Assert.Equal(ProjectFileStatus.Missing, service.FileStatus);

			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Born\", \"shortCircuit\": true }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal(ProjectFileStatus.Loaded, service.FileStatus);
			Assert.True(this._settings.Effective.Settings.Compiler.ShortCircuit);
		}

		[Fact]
		public async Task AnEditorThatSavesThroughATemporaryFileAndARename_IsOneChange()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder);
			String file = Path.Combine(folder, ProjectSettings.FileName);

			String temporary = Path.Combine(folder, "fss-project.json.tmp123");
			Edit(temporary, "{ \"name\": \"Renamed in\" }");
			File.Move(temporary, file, true);
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();
			await events.ExpectNoFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Renamed in", service.DisplayName);
			Assert.Equal(1, events.ProjectChanges);
		}

		[Fact]
		public async Task AnEditorThatDeletesAndRecreatesTheFile_IsAModification_NotADeletion()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder, Patient);
			String file = Path.Combine(folder, ProjectSettings.FileName);

			File.Delete(file);
			await Task.Delay(60);
			Edit(file, "{ \"name\": \"Recreated\" }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();
			await events.ExpectNoFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Recreated", service.DisplayName);
			Assert.Equal(ProjectFileStatus.Loaded, service.FileStatus);
		}

		[Fact]
		public async Task AHalfWrittenFile_IsNeverApplied_TheCompletedFileIs()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder, Fast with { RetryDelay = TimeSpan.FromMilliseconds(60), SettleAttempts = 7 });
			String file = Path.Combine(folder, ProjectSettings.FileName);

			using (FileStream stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
			{
				stream.Write(Encoding.UTF8.GetBytes("{ \"name\": \"Compl"));
				stream.Flush();
				await Task.Delay(100);
				Assert.Equal("First", service.DisplayName);
				stream.Write(Encoding.UTF8.GetBytes("ete\", \"optimization\": \"full\" }"));
				stream.Flush();
			}

			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();
			await events.ExpectNoFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Complete", service.DisplayName);
			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.Empty(events.Invalid);
		}

		[Fact]
		public async Task AFileThatIsLockedForAMoment_IsReadAgain()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder);
			String file = Path.Combine(folder, ProjectSettings.FileName);

			using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.None))
			{
				stream.SetLength(0);
				stream.Write(Encoding.UTF8.GetBytes("{ \"name\": \"After the lock\" }"));
				stream.Flush();
				await Task.Delay(250);
				Assert.Equal("First", service.DisplayName);
			}

			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("After the lock", service.DisplayName);
		}

		[Fact]
		public async Task InvalidJson_KeepsTheLastGoodSettings_AndReportsTheParseError()
		{
			String folder = this.MakeProject("{ \"name\": \"Good\", \"optimization\": \"full\" }");
			(ProjectService service, Recorder events) = this.Open(folder);
			String file = Path.Combine(folder, ProjectSettings.FileName);

			Edit(file, "{\r\n  \"name\": \"Broken\",\r\n  \"optimization\" \"none\"\r\n}");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Invalid, change.Kind);
			Assert.NotNull(change.Error);
			Assert.Equal(3, change.Error.Line);
			Assert.True(change.Error.Column > 0);
			Assert.Equal("Good", service.DisplayName);
			Assert.Equal(ProjectFileStatus.Loaded, service.FileStatus);
			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.Equal(0, events.ProjectChanges);

			await events.ExpectNoFileChangeAsync();
		}

		[Fact]
		public async Task AnInvalidFileThatIsFixedLater_IsAppliedThen()
		{
			String folder = this.MakeProject("{ \"name\": \"Good\" }");
			(ProjectService service, Recorder events) = this.Open(folder);
			String file = Path.Combine(folder, ProjectSettings.FileName);
			Edit(file, "{ nope");
			Assert.Equal(ProjectFileChangeKind.Invalid, (await events.NextFileChangeAsync()).Kind);

			Edit(file, "{ \"name\": \"Fixed\" }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Fixed", service.DisplayName);
		}

		[Fact]
		public async Task AFileThatIsEditedBackToWhatWasApplied_IsNotReloaded()
		{
			String folder = this.MakeProject("{ \"name\": \"Same\" }");
			(ProjectService service, Recorder events) = this.Open(folder);
			String file = Path.Combine(folder, ProjectSettings.FileName);

			Edit(file, "{ \"name\": \"Same\" }");
			File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(5));

			await events.ExpectNoFileChangeAsync();
			Assert.Equal("Same", service.DisplayName);
			Assert.Equal(0, events.ProjectChanges);
		}

		[Fact]
		public async Task DeletingTheFile_RevertsToAFolderWithoutSettings_AndRecreatingItAppliesAgain()
		{
			String folder = this.MakeProject("{ \"name\": \"Doomed\", \"shortCircuit\": true }");
			(ProjectService service, Recorder events) = this.Open(folder);
			String file = Path.Combine(folder, ProjectSettings.FileName);
			Assert.True(this._settings.Effective.Settings.Compiler.ShortCircuit);

			File.Delete(file);
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Deleted, change.Kind);
			Assert.True(service.IsOpen);
			Assert.Null(service.Settings);
			Assert.Equal(ProjectFileStatus.Missing, service.FileStatus);
			Assert.True(service.CanCreateSettings);
			Assert.Equal("mod", service.DisplayName);
			Assert.False(this._settings.Effective.Settings.Compiler.ShortCircuit);
			Assert.Same(this._settings.Settings, this._settings.Effective.Settings);

			Edit(file, "{ \"name\": \"Back\" }");
			change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Back", service.DisplayName);
		}

		[Fact]
		public async Task TheSafetyNet_NoticesAChangeTheFileSystemDidNotReport()
		{
			String folder = this.MakeProject();
			ProjectFileWatchOptions options = Fast with { UseFileSystemEvents = false };
			(ProjectService service, Recorder events) = this.Open(folder, options);

			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Found by polling\" }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Found by polling", service.DisplayName);
		}

		[Fact]
		public async Task AClosedProject_IsNoLongerWatched()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder);

			service.Close();
			events.Reset();
			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Ignored\" }");

			await events.ExpectNoFileChangeAsync();
			Assert.False(service.IsOpen);
			Assert.Equal(0, events.ProjectChanges);
		}

		[Fact]
		public async Task AProjectThatWasSwitchedAway_IsNoLongerWatched_TheNewOneIs()
		{
			String first = this.MakeProject();
			String second = this._root.Combine("other");
			Directory.CreateDirectory(second);
			Edit(Path.Combine(second, ProjectSettings.FileName), "{ \"name\": \"Other\" }");
			(ProjectService service, Recorder events) = this.Open(first);

			Assert.True(service.Open(second));
			events.Reset();
			Edit(Path.Combine(first, ProjectSettings.FileName), "{ \"name\": \"Old project edited\" }");
			await events.ExpectNoFileChangeAsync();
			Assert.Equal("Other", service.DisplayName);

			Edit(Path.Combine(second, ProjectSettings.FileName), "{ \"name\": \"Other edited\" }");
			ProjectFileChangedEventArgs change = await events.NextFileChangeAsync();

			Assert.Equal(ProjectFileChangeKind.Modified, change.Kind);
			Assert.Equal("Other edited", service.DisplayName);
		}

		[Fact]
		public async Task ADisposedService_StopsWatching()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder);

			service.Dispose();
			service.Dispose();
			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"After dispose\" }");

			await events.ExpectNoFileChangeAsync();
			Assert.Equal("First", service.DisplayName);
		}

		[Fact]
		public async Task ReloadingByHand_ReadsTheFileAndTheWatcherDoesNotReloadItAgain()
		{
			String folder = this.MakeProject();
			// A long quiet period: the manual reload happens well before the watcher looks at the file.
			(ProjectService service, Recorder events) = this.Open(folder, Fast with { Debounce = TimeSpan.FromMilliseconds(600), PollInterval = TimeSpan.Zero });
			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Manual\" }");
			service.Reload();

			await events.ExpectNoFileChangeAsync(TimeSpan.FromMilliseconds(1500));

			Assert.Equal("Manual", service.DisplayName);
			Assert.Equal(1, events.ProjectChanges);
		}

		[Fact]
		public void WithTheWatcherTurnedOff_NothingIsWatched()
		{
			String folder = this.MakeProject();
			(ProjectService service, Recorder events) = this.Open(folder, ProjectFileWatchOptions.Disabled);

			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Unseen\" }");
			Thread.Sleep(300);

			Assert.Equal("First", service.DisplayName);
			Assert.Equal(0, events.ProjectChanges);
			service.Reload();
			Assert.Equal("Unseen", service.DisplayName);
		}

		[Fact]
		public async Task TheChangesArePostedToTheSynchronizationContextOfTheService()
		{
			String folder = this.MakeProject();
			RecordingContext context = new RecordingContext();
			SynchronizationContext? previous = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext(context);
			ProjectService service;
			try
			{
				service = new ProjectService(this._settings, NullLogger<ProjectService>.Instance, Fast);
			}
			finally
			{
				SynchronizationContext.SetSynchronizationContext(previous);
			}

			this._disposables.Add(service);
			Assert.True(service.Open(folder));
			Recorder events = new Recorder(service);

			Edit(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Posted\" }");
			await context.NextPostAsync();
			Assert.Equal("First", service.DisplayName);

			context.RunPending();

			Assert.Equal("Posted", service.DisplayName);
			Assert.Equal(ProjectFileChangeKind.Modified, (await events.NextFileChangeAsync()).Kind);
		}

		/// <summary>Collects the events of a project service and lets a test wait for the next one.</summary>
		private sealed class Recorder
		{
			private readonly ConcurrentQueue<ProjectFileChangedEventArgs> _changes = new ConcurrentQueue<ProjectFileChangedEventArgs>();
			private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
			private Int32 _projectChanges;

			public Recorder(ProjectService service)
			{
				service.ProjectChanged += (_, _) => Interlocked.Increment(ref this._projectChanges);
				service.FileChangedOnDisk += (_, e) =>
				{
					this._changes.Enqueue(e);
					_ = this._signal.Release();
				};
			}

			public Int32 ProjectChanges => Volatile.Read(ref this._projectChanges);

			public IEnumerable<ProjectFileChangedEventArgs> Invalid
			{
				get
				{
					foreach (ProjectFileChangedEventArgs change in this._changes)
					{
						if (change.Kind == ProjectFileChangeKind.Invalid)
						{
							yield return change;
						}
					}
				}
			}

			public void Reset()
			{
				while (this._changes.TryDequeue(out _))
				{
					// Drain.
				}

				while (this._signal.Wait(0))
				{
					// Drain.
				}

				Interlocked.Exchange(ref this._projectChanges, 0);
			}

			public async Task<ProjectFileChangedEventArgs> NextFileChangeAsync()
			{
				Assert.True(await this._signal.WaitAsync(Patience), "The project file change was not noticed.");
				Assert.True(this._changes.TryDequeue(out ProjectFileChangedEventArgs? change));
				return change;
			}

			public async Task ExpectNoFileChangeAsync(TimeSpan? wait = null)
			{
				Boolean unexpected = await this._signal.WaitAsync(wait ?? Quiet);
				Assert.False(unexpected, "A change was reported that should have been ignored: " + (this._changes.TryPeek(out ProjectFileChangedEventArgs? change) ? change.Kind.ToString() : "?"));
			}
		}

		/// <summary>A synchronization context that keeps what is posted to it until the test runs it.</summary>
		private sealed class RecordingContext : SynchronizationContext
		{
			private readonly ConcurrentQueue<(SendOrPostCallback Callback, Object? State)> _posts = new ConcurrentQueue<(SendOrPostCallback, Object?)>();
			private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

			public override void Post(SendOrPostCallback d, Object? state)
			{
				this._posts.Enqueue((d, state));
				_ = this._signal.Release();
			}

			public async Task NextPostAsync()
			{
				Assert.True(await this._signal.WaitAsync(Patience), "Nothing was posted.");
			}

			public void RunPending()
			{
				while (this._posts.TryDequeue(out (SendOrPostCallback Callback, Object? State) post))
				{
					post.Callback(post.State);
				}
			}
		}
	}
}