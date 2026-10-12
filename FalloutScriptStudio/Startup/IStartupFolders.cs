namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Collections.Generic;

	/// <summary>The folders the program writes to: creation at the start, scratch folder preparation and deletion. None of them throws for a file system problem.</summary>
	public interface IStartupFolders
	{
		/// <summary>Creates the settings, templates and scratch folders.</summary>
		/// <returns>The problems that were met (empty when everything worked).</returns>
		IReadOnlyList<String> EnsureCreated();

		/// <summary>Creates the scratch folder or deletes the files a previous session left in it.</summary>
		/// <returns>The problems that were met (empty when everything worked).</returns>
		IReadOnlyList<String> PrepareScratch();

		/// <summary>Deletes the scratch folder with everything in it.</summary>
		/// <returns>The problems that were met (empty when everything worked).</returns>
		IReadOnlyList<String> DeleteScratch();
	}
}
