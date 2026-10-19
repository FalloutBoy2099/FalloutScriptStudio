namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>
	/// An in-memory <see cref="ISettingsService"/>.
	/// </summary>
	internal sealed class FakeSettingsService : ISettingsService
	{
		/// <summary>Initializes a new instance of the <see cref="FakeSettingsService"/> class.</summary>
		/// <param name="baseDirectory">The program folder of the fake installation (a scratch folder of the test).</param>
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

		public Int32 SaveCount { get; private set; }

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
			this.Save();
			return Task.CompletedTask;
		}

		public void Apply(AppSettings settings)
		{
			this.Settings = settings;
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(settings));
		}

		public void Update(Action<AppSettings> update)
		{
			update(this.Settings);
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(this.Settings));
		}

		public void AddRecentScript(String path)
		{
			this.Recent.AddScript(path);
		}
	}
}