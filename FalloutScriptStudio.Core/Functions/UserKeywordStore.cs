namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// <see cref="IUserKeywordStore"/> that keeps the keywords in a small JSON file.
	/// </summary>
	/// <remarks>
	/// File format (<c>user-keywords.json</c>, written atomically): <c>{ "keywords": [ "DEBUGMSG", "MyMacro" ] }</c>.
	/// The legacy <c>User_SyntaxRules.xml</c> can be imported once with <see cref="ImportLegacyXmlAsync"/>.
	/// </remarks>
	public sealed class UserKeywordStore : IUserKeywordStore, IDisposable
	{
		private readonly String _filePath;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
		private IReadOnlyList<String> _keywords = Array.Empty<String>();

		/// <summary>
		/// Initializes a new instance of the <see cref="UserKeywordStore"/> class.
		/// </summary>
		/// <param name="filePath">Full path of the JSON file.</param>
		public UserKeywordStore(String filePath)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);
			this._filePath = filePath;
		}

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public IReadOnlyList<String> Keywords => Volatile.Read(ref this._keywords);

		/// <summary>
		/// Reads the keyword entries of a legacy <c>User_SyntaxRules.xml</c>: the <c>Key word="…"</c> items of the
		/// <c>KeyWords name="UserMacros"</c> section. Empty and invalid entries are skipped.
		/// </summary>
		/// <param name="xml">The content of the file.</param>
		/// <returns>The keywords in file order, without duplicates.</returns>
		/// <exception cref="System.Xml.XmlException">The text is not valid XML.</exception>
		public static IReadOnlyList<String> ParseLegacyXml(String xml)
		{
			ArgumentNullException.ThrowIfNull(xml);

			XDocument document = XDocument.Parse(xml);
			IEnumerable<String> words = document.Descendants("KeyWords")
				.Where(e => String.Equals((String?)e.Attribute("name"), "UserMacros", StringComparison.Ordinal))
				.Descendants("Key")
				.Select(k => ((String?)k.Attribute("word") ?? String.Empty).Trim());
			return words.Where(IsValidKeyword).Distinct(StringComparer.Ordinal).ToList();
		}

		/// <summary>Determines whether a string can be a keyword (a non-empty identifier).</summary>
		/// <param name="keyword">The string.</param>
		/// <returns><c>true</c> for letters, digits and underscores.</returns>
		public static Boolean IsValidKeyword(String keyword)
		{
			return !String.IsNullOrEmpty(keyword) && ProcedureNameParser.IsIdentifier(keyword);
		}

		/// <inheritdoc />
		public async Task LoadAsync(CancellationToken cancellationToken = default)
		{
			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				Volatile.Write(ref this._keywords, await this.ReadFileAsync(cancellationToken).ConfigureAwait(false));
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc />
		public async Task<Boolean> AddAsync(String keyword, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(keyword);

			String word = keyword.Trim();
			if (!IsValidKeyword(word))
			{
				return false;
			}

			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				List<String> list = this.Keywords.ToList();
				if (list.Contains(word, StringComparer.Ordinal))
				{
					return false;
				}

				list.Add(word);
				await this.SaveAsync(list, cancellationToken).ConfigureAwait(false);
				Volatile.Write(ref this._keywords, list);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		/// <inheritdoc />
		public async Task<Boolean> RemoveAsync(String keyword, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(keyword);

			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				List<String> list = this.Keywords.ToList();
				if (list.RemoveAll(k => String.Equals(k, keyword.Trim(), StringComparison.Ordinal)) == 0)
				{
					return false;
				}

				await this.SaveAsync(list, cancellationToken).ConfigureAwait(false);
				Volatile.Write(ref this._keywords, list);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		/// <summary>
		/// Adds the keywords of a legacy <c>User_SyntaxRules.xml</c> to the list.
		/// </summary>
		/// <param name="legacyXmlPath">Path of the legacy file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The number of keywords that were added; 0 when the file is missing or is not valid XML.</returns>
		public async Task<Int32> ImportLegacyXmlAsync(String legacyXmlPath, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(legacyXmlPath);

			IReadOnlyList<String> imported;
			try
			{
				imported = ParseLegacyXml(await File.ReadAllTextAsync(legacyXmlPath, cancellationToken).ConfigureAwait(false));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
			{
				return 0;
			}

			Int32 added = 0;
			foreach (String word in imported)
			{
				if (await this.AddAsync(word, cancellationToken).ConfigureAwait(false))
				{
					added++;
				}
			}

			return added;
		}

		private async Task<IReadOnlyList<String>> ReadFileAsync(CancellationToken cancellationToken)
		{
			if (!File.Exists(this._filePath))
			{
				return Array.Empty<String>();
			}

			try
			{
				String json = await File.ReadAllTextAsync(this._filePath, cancellationToken).ConfigureAwait(false);
				KeywordFile? file = JsonSerializer.Deserialize<KeywordFile>(json, SettingsJson.Options);
				return (file?.Keywords ?? new List<String>())
					.Select(k => (k ?? String.Empty).Trim())
					.Where(IsValidKeyword)
					.Distinct(StringComparer.Ordinal)
					.ToList();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
			{
				return Array.Empty<String>();
			}
		}

		private Task SaveAsync(List<String> keywords, CancellationToken cancellationToken)
		{
			Byte[] content = JsonSerializer.SerializeToUtf8Bytes(new KeywordFile { Keywords = keywords }, SettingsJson.Options);
			return AtomicFile.WriteAllBytesAsync(this._filePath, content, cancellationToken);
		}

		private sealed class KeywordFile
		{
			public List<String> Keywords { get; set; } = new List<String>();
		}

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}
