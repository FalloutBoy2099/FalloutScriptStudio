namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Threading.Tasks;

	/// <summary>The services the start-up sequence and the exit touch, in the order of the sequence.</summary>
	public interface IStartupServices : IDisposable
	{
		/// <summary>Gets the container the host takes the windows from.</summary>
		IServiceProvider Provider { get; }

		/// <summary>Reads the settings from the files (the legacy files are imported automatically).</summary>
		void LoadSettings();

		/// <summary>Writes the settings to the files.</summary>
		/// <exception cref="System.IO.IOException">A file could not be written.</exception>
		/// <exception cref="UnauthorizedAccessException">A file could not be written.</exception>
		void SaveSettings();

		/// <summary>Creates the project service, which reads the project of the solution folder before any document is parsed or compiled.</summary>
		void ActivateProject();

		/// <summary>Reads the user keywords, the function catalog, the dialogue rules and the node snippets.</summary>
		/// <returns>A task that completes when all of them are loaded.</returns>
		Task LoadStoresAsync();

		/// <summary>Applies the color mode the application starts with.</summary>
		void ApplyStartupTheme();

		/// <summary>Gets the language stored in the shell layout.</summary>
		/// <returns>The language code, or <see langword="null"/> when none is stored.</returns>
		String? GetStoredLanguage();
	}
}
