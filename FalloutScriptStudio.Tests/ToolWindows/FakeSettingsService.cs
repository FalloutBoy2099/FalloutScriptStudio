namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;

	internal sealed class FakeSettingsService : ISettingsService
	{
		public FakeSettingsService(String baseDirectory)
		{
			this.Paths = AppPaths.FromBaseDirectory(baseDirectory);
		}

		public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

		public AppPaths Paths { get; }

		public AppSettings Settings { get; private set; } = new AppSettings();

		public RecentFiles Recent { get; } = new RecentFiles();

		public ScriptPositions ScriptPositions { get; } = new ScriptPositions();

		public FoldedProcedures FoldedProcedures { get; } = new FoldedProcedures();

		public SearchHistory SearchHistory { get; } = new SearchHistory();

		public PreprocessorDefines PreprocessorDefines { get; } = new PreprocessorDefines();

		public Boolean ImportedLegacyData => false;

		public Int32 ApplyCount { get; private set; }

		public Int32 UpdateCount { get; private set; }

		public Int32 SaveCount { get; private set; }

		public Exception? SaveFailure { get; set; }

		public void Load()
		{
		}

		public Task LoadAsync(CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public void Save()
		{
			this.SaveCount++;
		}

		public Task SaveAsync(CancellationToken cancellationToken = default)
		{
			if (this.SaveFailure != null)
			{
				return Task.FromException(this.SaveFailure);
			}

			this.SaveCount++;
			return Task.CompletedTask;
		}

		public void Apply(AppSettings settings)
		{
			settings.Normalize();
			this.Settings = settings;
			this.ApplyCount++;
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(settings));
		}

		public void Update(Action<AppSettings> update)
		{
			update(this.Settings);
			this.UpdateCount++;
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(this.Settings));
		}

		public void AddRecentScript(String path)
		{
			this.Recent.AddScript(path);
		}
	}
}
