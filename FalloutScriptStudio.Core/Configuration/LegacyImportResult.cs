namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The data imported from the files of the old editor. A member is <see langword="null"/> when the corresponding legacy file does not exist.
	/// </summary>
	/// <param name="Settings">The imported settings (from <c>settings.dat</c>).</param>
	/// <param name="Recent">The imported recent files (from <c>recent.dat</c>).</param>
	/// <param name="Positions">The imported script positions (from <c>recent.dat</c>).</param>
	/// <param name="Folding">The imported folded procedures (from <c>folding.dat</c>).</param>
	/// <param name="SearchHistory">The imported search history and folders (from the <c>.ini</c> files).</param>
	/// <param name="Defines">The imported preprocessor definitions (from <c>PreprocDefine.ini</c>).</param>
	/// <param name="Problems">Descriptions of files that were damaged or only partly readable.</param>
	public sealed record LegacyImportResult(
		AppSettings? Settings,
		RecentFiles? Recent,
		ScriptPositions? Positions,
		FoldedProcedures? Folding,
		SearchHistory? SearchHistory,
		PreprocessorDefines? Defines,
		IReadOnlyList<String> Problems)
	{
		/// <summary>Gets a value indicating whether any legacy file was found.</summary>
		public Boolean FoundAnything => this.Settings != null
			|| this.Recent != null
			|| this.Folding != null
			|| this.SearchHistory != null
			|| this.Defines != null;
	}
}