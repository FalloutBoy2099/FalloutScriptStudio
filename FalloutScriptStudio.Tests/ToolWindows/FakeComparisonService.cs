namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;

	/// <summary>The files side of the comparison in memory: the files are lists of lines by path, and what the view model asks to write is recorded.</summary>
	internal sealed class FakeComparisonService : IMessageComparisonService
	{
		public Dictionary<String, String[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

		public List<MessageLanguageFile> Siblings { get; } = [];

		public HashSet<String> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

		public List<(String Path, Int32[] Numbers, String? Marker)> Added { get; } = [];

		public List<(String Path, String Content)> Reports { get; } = [];

		public List<(String Path, Boolean IsReference)> Loads { get; } = [];

		public IReadOnlyList<Int32> NotEncodable { get; set; } = Array.Empty<Int32>();

		public Task<LoadedMessageFile> LoadAsync(String path, Encoding configured, Boolean isReference, CancellationToken cancellationToken = default)
		{
			String full = Path.GetFullPath(path);
			this.Loads.Add((full, isReference));
			if (this.Unreadable.Contains(full) || !this.Files.TryGetValue(full, out String[]? lines))
			{
				throw new FileNotFoundException("not readable", full);
			}

			DecodedMessageFile decoded = new(lines, TextEncodings.FromCodePage(1252), false, "\r\n", true, false, false);
			return Task.FromResult(new LoadedMessageFile(full, MessageLanguages.DetectLanguage(full), decoded, MessageDocument.Parse(lines)));
		}

		public MessageComparison Compare(LoadedMessageFile reference, LoadedMessageFile target)
		{
			return MessageFileComparer.Compare(reference.Document, target.Document);
		}

		public IReadOnlyList<MessageLanguageFile> FindSiblings(String path)
		{
			return this.Siblings;
		}

		public Task<MessageAppendResult> AddMissingAsync(LoadedMessageFile target, IEnumerable<MessageEntry> messages, String? marker, CancellationToken cancellationToken = default)
		{
			List<MessageEntry> entries = messages.ToList();
			this.Added.Add((target.Path, entries.Select(entry => entry.Id).ToArray(), marker));
			List<Int32> added = entries.Select(entry => entry.Id).Where(id => !this.NotEncodable.Contains(id)).ToList();
			this.Files[target.Path] = this.Files[target.Path].Concat(entries.Where(entry => added.Contains(entry.Id)).Select(entry => entry.Format().Replace("\r\n", "\n", StringComparison.Ordinal))).ToArray();
			return Task.FromResult(new MessageAppendResult(added, this.NotEncodable));
		}

		public Task WriteReportAsync(String path, String content, CancellationToken cancellationToken = default)
		{
			this.Reports.Add((path, content));
			return Task.CompletedTask;
		}
	}
}