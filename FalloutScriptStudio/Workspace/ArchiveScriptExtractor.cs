namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;

	/// <summary>Extracts scripts from Fallout game archives (<c>.dat</c>) and remembers the archive that was used last.</summary>
	public sealed class ArchiveScriptExtractor
	{
		private readonly ISettingsService _settings;
		private readonly IDatArchiveExtractor _archives;
		private readonly IDialogService _dialogs;
		private readonly IProblemService _problems;
		private String? _lastArchive;

		/// <summary>Initializes the extractor.</summary>
		public ArchiveScriptExtractor(ISettingsService settings, IDatArchiveExtractor archives, IDialogService dialogs, IProblemService? problems = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(archives);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._settings = settings;
			this._archives = archives;
			this._dialogs = dialogs;
			this._problems = problems ?? new ProblemService();
		}

		/// <summary>
		/// Extracts a script into the scratch folder. Asks for the archive when no archive was used before (or it vanished)
		/// and shows an error when the extraction fails.
		/// </summary>
		/// <returns>The path of the extracted file, or <see langword="null"/> when the user cancelled or the extraction failed.</returns>
		public async Task<String?> ExtractAsync(String scriptFileName)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptFileName);

			String? archive = this._lastArchive;
			if (archive is null || !File.Exists(archive))
			{
				archive = this._dialogs.PickOpenFile(Loc.Get("Str.Shell.Dlg.SelectArchiveTitle"), Loc.Get("Str.Shell.Filter.Archive"), this._settings.Paths.ProgramDirectory);
				if (archive is null)
				{
					return null;
				}
			}

			DatExtractionResult result = await this._archives.ExtractScriptAsync(new DatExtractionRequest
			{
				DatFilePath = archive,
				ScriptFileName = scriptFileName,
				OutputDirectory = this._settings.Paths.ScratchDirectory,
			});
			if (!result.Success || result.ExtractedFilePath is null)
			{
				this._lastArchive = null;
				this._problems.Report(ProblemFactory.ExtractFailed(scriptFileName, result.Message));
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ExtractionFailedTitle"), result.Message);
				return null;
			}

			this._lastArchive = archive;
			return result.ExtractedFilePath;
		}
	}
}
