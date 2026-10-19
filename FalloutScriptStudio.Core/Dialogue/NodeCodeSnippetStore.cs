namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Configuration;

	/// <summary>
	/// <see cref="INodeCodeSnippetStore"/> that keeps the snippets in a text file, one <c>name|code</c> line per snippet.
	/// </summary>
	/// <remarks>
	/// Line breaks of a snippet are written as <c>&lt;cr&gt;</c> (the legacy editor wrote them raw, which broke the file); the code of a
	/// legacy line is everything after the first <c>|</c>. A line without a <c>|</c> that follows a snippet is read as the continuation of
	/// that snippet (how the legacy editor stored multi-line code), so the snippets of a legacy file survive the first save. The store never
	/// writes the file before it was read: <see cref="AddAsync"/> and <see cref="RemoveAsync"/> load it first when needed.
	/// The asynchronous methods resume on the caller's synchronization context, so <see cref="Changed"/> is raised on the thread
	/// that called them (the UI thread for the application).
	/// </remarks>
	public sealed class NodeCodeSnippetStore : INodeCodeSnippetStore, IDisposable
	{
		private const String LineBreakMarker = "<cr>";

		private readonly String _filePath;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
		private IReadOnlyList<NodeCodeSnippet> _snippets = Array.Empty<NodeCodeSnippet>();
		private Boolean _loaded;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeCodeSnippetStore"/> class.
		/// </summary>
		/// <param name="filePath">Full path of the snippet file (<c>settings\CustomCode.ini</c>).</param>
		public NodeCodeSnippetStore(String filePath)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);
			this._filePath = filePath;
		}

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public IReadOnlyList<NodeCodeSnippet> Snippets => Volatile.Read(ref this._snippets);

		/// <summary>Parses the lines of a snippet file.</summary>
		/// <param name="lines">The lines.</param>
		/// <returns>The snippets in file order.</returns>
		public static IReadOnlyList<NodeCodeSnippet> Parse(IEnumerable<String> lines)
		{
			ArgumentNullException.ThrowIfNull(lines);

			var snippets = new List<NodeCodeSnippet>();
			Int32 pendingBlankLines = 0;
			foreach (String line in lines)
			{
				Int32 bar = line.IndexOf('|', StringComparison.Ordinal);
				if (bar < 0 && snippets.Count > 0)
				{
					// A raw continuation line of a multi-line snippet written by the legacy editor; blank lines count only when more code follows.
					if (String.IsNullOrWhiteSpace(line))
					{
						pendingBlankLines++;
						continue;
					}

					NodeCodeSnippet previous = snippets[^1];
					snippets[^1] = previous with { Code = previous.Code + String.Concat(Enumerable.Repeat(Environment.NewLine, pendingBlankLines + 1)) + line };
					pendingBlankLines = 0;
					continue;
				}

				pendingBlankLines = 0;
				if (bar <= 0)
				{
					continue;
				}

				snippets.Add(new NodeCodeSnippet(line.Substring(0, bar), line.Substring(bar + 1).Replace(LineBreakMarker, Environment.NewLine, StringComparison.Ordinal)));
			}

			return snippets;
		}

		/// <inheritdoc />
		public async Task LoadAsync(CancellationToken cancellationToken = default)
		{
			await this._gate.WaitAsync(cancellationToken);
			try
			{
				await this.ReadFileAsync(cancellationToken);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc />
		public async Task<Boolean> AddAsync(String name, String code, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(code);

			String cleanName = name.Replace('|', '/').Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Trim();
			if (cleanName.Length == 0 || code.Length == 0)
			{
				return false;
			}

			await this._gate.WaitAsync(cancellationToken);
			try
			{
				if (!await this.EnsureLoadedAsync(cancellationToken))
				{
					return false;
				}

				List<NodeCodeSnippet> list = this.Snippets.Where(s => !String.Equals(s.Name, cleanName, StringComparison.Ordinal)).ToList();
				list.Add(new NodeCodeSnippet(cleanName, code));
				await this.SaveAsync(list, cancellationToken);
				Volatile.Write(ref this._snippets, list);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		/// <inheritdoc />
		public async Task<Boolean> RemoveAsync(String name, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(name);

			await this._gate.WaitAsync(cancellationToken);
			try
			{
				if (!await this.EnsureLoadedAsync(cancellationToken))
				{
					return false;
				}

				List<NodeCodeSnippet> list = this.Snippets.ToList();
				if (list.RemoveAll(s => String.Equals(s.Name, name, StringComparison.Ordinal)) == 0)
				{
					return false;
				}

				await this.SaveAsync(list, cancellationToken);
				Volatile.Write(ref this._snippets, list);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		private async Task<Boolean> EnsureLoadedAsync(CancellationToken cancellationToken)
		{
			if (!this._loaded)
			{
				await this.ReadFileAsync(cancellationToken).ConfigureAwait(false);
			}

			return this._loaded;
		}

		private async Task ReadFileAsync(CancellationToken cancellationToken)
		{
			IReadOnlyList<NodeCodeSnippet> snippets = Array.Empty<NodeCodeSnippet>();
			Boolean readable = true;
			if (File.Exists(this._filePath))
			{
				try
				{
					snippets = Parse(await File.ReadAllLinesAsync(this._filePath, cancellationToken).ConfigureAwait(false));
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					readable = false;
				}
			}

			Volatile.Write(ref this._snippets, snippets);
			this._loaded = readable;
		}

		private Task SaveAsync(IReadOnlyList<NodeCodeSnippet> snippets, CancellationToken cancellationToken)
		{
			String content = String.Concat(snippets.Select(s =>
				s.Name + "|" + s.Code.Replace("\r\n", LineBreakMarker, StringComparison.Ordinal).Replace("\n", LineBreakMarker, StringComparison.Ordinal).Replace("\r", LineBreakMarker, StringComparison.Ordinal) + "\r\n"));
			return AtomicFile.WriteAllBytesAsync(this._filePath, new UTF8Encoding(false).GetBytes(content), cancellationToken);
		}

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}