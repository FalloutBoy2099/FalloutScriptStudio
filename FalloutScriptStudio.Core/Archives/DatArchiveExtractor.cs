namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="IDatArchiveExtractor"/> implementation built on <see cref="IDatArchiveReader"/>: it extracts
	/// <c>scripts\&lt;name&gt;</c> from the archive without any external program.
	/// </summary>
	public sealed class DatArchiveExtractor : IDatArchiveExtractor
	{
		private const String ScriptsDirectory = @"scripts\";

		private readonly ILogger<DatArchiveExtractor> _logger;
		private readonly IDatArchiveReaderFactory _readers;

		/// <summary>
		/// Initializes a new instance of the <see cref="DatArchiveExtractor"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="readers">Opens the archives; the standard factory when omitted.</param>
		public DatArchiveExtractor(ILogger<DatArchiveExtractor> logger, IDatArchiveReaderFactory? readers = null)
		{
			ArgumentNullException.ThrowIfNull(logger);
			this._logger = logger;
			this._readers = readers ?? new DatArchiveReaderFactory();
		}

		/// <inheritdoc/>
		public async Task<DatExtractionResult> ExtractScriptAsync(DatExtractionRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			if (!File.Exists(request.DatFilePath) && !Directory.Exists(request.DatFilePath))
			{
				return new DatExtractionResult(false, null, "The archive file was not found: " + request.DatFilePath);
			}

			String scriptName = GetLeafName(request.ScriptFileName);
			if (scriptName.Length == 0)
			{
				return new DatExtractionResult(false, null, "No script name was specified.");
			}

			try
			{
				IDatArchiveReader archive = await this._readers.OpenAsync(request.DatFilePath, cancellationToken).ConfigureAwait(false);
				DatEntry? entry = archive.Find(ScriptsDirectory + scriptName);
				if (entry is null)
				{
					return new DatExtractionResult(false, null, "The script was not found in the archive.");
				}

				Directory.CreateDirectory(request.OutputDirectory);
				String extracted = Path.Combine(request.OutputDirectory, scriptName);
				await archive.ExtractToFileAsync(entry, extracted, cancellationToken).ConfigureAwait(false);
				return new DatExtractionResult(true, extracted, String.Empty);
			}
			catch (DatException exception)
			{
				this._logger.ScriptNotReadable(exception, scriptName, request.DatFilePath);
				return new DatExtractionResult(false, null, "The archive could not be read: " + exception.Message);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				this._logger.ScriptNotExtracted(exception, scriptName, request.DatFilePath);
				return new DatExtractionResult(false, null, "The script could not be extracted: " + exception.Message);
			}
		}

		/// <summary>Gets the file name of a path that may use backslashes or slashes.</summary>
		private static String GetLeafName(String path)
		{
			return path.Substring(path.LastIndexOfAny(['\\', '/']) + 1);
		}
	}
}
