namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Snippets;

	/// <summary>An in-memory <see cref="ISnippetService"/>: the files are lists of definitions, and a save can be made to fail.</summary>
	internal sealed class FakeSnippetService : ISnippetService
	{
		private readonly Dictionary<SnippetSource, SnippetFileReadResult> _files = [];

		public event EventHandler? Changed;

		public String UserFilePath { get; set; } = @"C:\settings\snippets.json";

		public String? ProjectFilePath { get; set; }

		/// <summary>Gets the lists of the last successful saves.</summary>
		public Dictionary<SnippetSource, List<SnippetDefinition>> Saved { get; } = [];

		/// <summary>Gets or sets the error the next save reports, or <see langword="null"/> for a save that works.</summary>
		public String? SaveError { get; set; }

		public IReadOnlyList<Snippet> GetSnippets()
		{
			return SnippetCatalog.Merge(
				BuiltInSnippets.All,
				this.GetFile(SnippetSource.User).File.Snippets.Select(definition => definition.ToSnippet(SnippetSource.User)),
				this.ProjectFilePath is null ? null : this.GetFile(SnippetSource.Project).File.Snippets.Select(definition => definition.ToSnippet(SnippetSource.Project)));
		}

		public SnippetFileReadResult GetFile(SnippetSource source)
		{
			return this._files.TryGetValue(source, out SnippetFileReadResult? result) ? result : new SnippetFileReadResult(SnippetFileStatus.Missing, new SnippetFile());
		}

		/// <summary>Sets what a file contains.</summary>
		public void SetFile(SnippetSource source, SnippetFileStatus status, params SnippetDefinition[] snippets)
		{
			this._files[source] = new SnippetFileReadResult(status, new SnippetFile { Snippets = snippets.ToList() }, status == SnippetFileStatus.Loaded || status == SnippetFileStatus.Missing ? null : "broken");
		}

		public Boolean TrySave(SnippetSource source, IReadOnlyList<SnippetDefinition> snippets, out String? error)
		{
			error = this.SaveError;
			if (this.SaveError is not null || this.GetFile(source).Status == SnippetFileStatus.Unreadable)
			{
				error ??= "unreadable";
				return false;
			}

			this.Saved[source] = snippets.Select(snippet => snippet.Clone()).ToList();
			this._files[source] = new SnippetFileReadResult(SnippetFileStatus.Loaded, new SnippetFile { Snippets = this.Saved[source] });
			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		public void Reload()
		{
			this.Changed?.Invoke(this, EventArgs.Empty);
		}
	}
}