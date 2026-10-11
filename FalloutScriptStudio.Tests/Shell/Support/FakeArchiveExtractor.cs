namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Archives;

	/// <summary>An archive extractor that writes a canned file instead of running <c>dat2.exe</c>.</summary>
	internal sealed class FakeArchiveExtractor : IDatArchiveExtractor
	{
		public String? Content { get; set; }

		public DatExtractionRequest? LastRequest { get; private set; }

		public String BuildCommandLine(DatExtractionRequest request)
		{
			return request.ScriptFileName;
		}

		public Task<DatExtractionResult> ExtractScriptAsync(DatExtractionRequest request, CancellationToken cancellationToken = default)
		{
			this.LastRequest = request;
			if (this.Content is null)
			{
				return Task.FromResult(new DatExtractionResult(false, null, "not found in the archive"));
			}

			Directory.CreateDirectory(request.OutputDirectory);
			String file = Path.Combine(request.OutputDirectory, Path.GetFileName(request.ScriptFileName));
			File.WriteAllText(file, this.Content);
			return Task.FromResult(new DatExtractionResult(true, file, "ok"));
		}
	}
}
