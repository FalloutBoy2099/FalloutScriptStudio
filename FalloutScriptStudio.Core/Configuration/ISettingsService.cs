namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Owns the persisted user data: the settings and the small side models (recent files, script positions, folding, search history).
	/// </summary>
	public interface ISettingsService
	{
		/// <summary>Occurs after the settings were loaded, replaced with <see cref="Apply"/> or changed with <see cref="Update"/>.</summary>
		event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

		/// <summary>Gets the paths of the installation.</summary>
		AppPaths Paths { get; }

		/// <summary>Gets the current settings. Never <see langword="null"/>, even before <see cref="Load"/> (then it holds the defaults).</summary>
		AppSettings Settings { get; }

		/// <summary>Gets the recent script and message files.</summary>
		RecentFiles Recent { get; }

		/// <summary>Gets the remembered caret lines of scripts.</summary>
		ScriptPositions ScriptPositions { get; }

		/// <summary>Gets the folded procedures of scripts.</summary>
		FoldedProcedures FoldedProcedures { get; }

		/// <summary>Gets the search history and the search folders.</summary>
		SearchHistory SearchHistory { get; }

		/// <summary>Gets the preprocessor definitions list.</summary>
		PreprocessorDefines PreprocessorDefines { get; }

		/// <summary>Gets a value indicating whether the last load took its data from the files of the old editor.</summary>
		Boolean ImportedLegacyData { get; }

		/// <summary>
		/// Loads everything from disk. Missing or damaged files never throw; the defaults are used and the problem is logged.
		/// A damaged file is kept next to the original with the extension <c>.corrupt</c>.
		/// When no new settings file exists but legacy files do, they are imported.
		/// </summary>
		void Load();

		/// <summary>Asynchronous version of <see cref="Load"/>.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when loading is finished.</returns>
		Task LoadAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes everything to disk (each file atomically).
		/// </summary>
		/// <exception cref="System.IO.IOException">A file could not be written (the problem is also logged).</exception>
		/// <exception cref="UnauthorizedAccessException">A file could not be written (the problem is also logged).</exception>
		void Save();

		/// <summary>Asynchronous version of <see cref="Save"/>.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when everything is written.</returns>
		Task SaveAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Replaces the current settings (typically with an edited <see cref="AppSettings.Clone"/>) and raises <see cref="SettingsChanged"/>.
		/// </summary>
		/// <param name="settings">The new settings.</param>
		void Apply(AppSettings settings);

		/// <summary>
		/// Modifies the current settings in place and raises <see cref="SettingsChanged"/>.
		/// </summary>
		/// <param name="update">The modification.</param>
		void Update(Action<AppSettings> update);

		/// <summary>
		/// Adds a script to the recent files. When the oldest entry is dropped its remembered caret line is forgotten too.
		/// </summary>
		/// <param name="path">The script path.</param>
		void AddRecentScript(String path);
	}
}
