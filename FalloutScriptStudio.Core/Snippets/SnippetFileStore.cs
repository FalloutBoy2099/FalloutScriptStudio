namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.IO;
	using System.Text;
	using System.Text.Json;
	using System.Threading;
	using Configuration;

	/// <summary>
	/// Reads and writes a snippet file with the safety rules of the other files of the settings: a write goes through a temporary file, a
	/// version of the layout and the properties this version does not know are kept, comments and trailing commas are tolerated, a damaged
	/// file is copied to <c>.corrupt</c>, and a file that cannot be read is never overwritten.
	/// </summary>
	public static class SnippetFileStore
	{
		private const Int32 ReadAttempts = 3;
		private const Int32 RetryDelayMilliseconds = 40;

		/// <summary>Reads a snippet file.</summary>
		/// <param name="path">The full path.</param>
		/// <returns>What was found.</returns>
		public static SnippetFileReadResult Read(String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			Byte[] content;
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					if (!File.Exists(path))
					{
						return new SnippetFileReadResult(SnippetFileStatus.Missing, new SnippetFile());
					}

					content = File.ReadAllBytes(path);
					break;
				}
				catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
				{
					return new SnippetFileReadResult(SnippetFileStatus.Missing, new SnippetFile());
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					if (attempt >= ReadAttempts)
					{
						return new SnippetFileReadResult(SnippetFileStatus.Unreadable, new SnippetFile(), ex.Message);
					}

					Thread.Sleep(RetryDelayMilliseconds);
				}
			}

			if (TryParse(content, out SnippetFile? file, out String? error))
			{
				return new SnippetFileReadResult(SnippetFileStatus.Loaded, file!);
			}

			KeepCorruptCopy(path);
			return new SnippetFileReadResult(SnippetFileStatus.Damaged, new SnippetFile(), error);
		}

		/// <summary>Interprets the content of a snippet file (nothing is read from or written to the disk).</summary>
		/// <param name="content">The bytes of the file.</param>
		/// <param name="file">The file when the content is valid.</param>
		/// <param name="error">Why the content is not valid.</param>
		/// <returns><see langword="true"/> when the content is valid.</returns>
		public static Boolean TryParse(Byte[] content, out SnippetFile? file, out String? error)
		{
			ArgumentNullException.ThrowIfNull(content);

			file = null;
			error = null;
			try
			{
				SnippetFile? parsed = JsonSerializer.Deserialize<SnippetFile>(ToUtf8(content), SettingsJson.Options);
				if (parsed is null)
				{
					error = "The file does not contain a snippet list.";
					return false;
				}

				parsed.Snippets ??= [];
				parsed.Snippets.RemoveAll(snippet => snippet is null);
				foreach (SnippetDefinition snippet in parsed.Snippets)
				{
					snippet.Trigger ??= String.Empty;
					snippet.Description ??= String.Empty;
					snippet.Body ??= String.Empty;
				}

				file = parsed;
				return true;
			}
			catch (JsonException ex)
			{
				error = ex.Message;
				return false;
			}
		}

		/// <summary>Creates the bytes to write for a snippet file.</summary>
		/// <param name="file">The content.</param>
		/// <returns>The bytes (UTF-8 without a byte order mark).</returns>
		public static Byte[] Serialize(SnippetFile file)
		{
			ArgumentNullException.ThrowIfNull(file);

			SnippetFile copy = file.Clone();
			copy.Version = Math.Max(copy.Version, SnippetFile.CurrentVersion);
			return JsonSerializer.SerializeToUtf8Bytes(copy, SettingsJson.Options);
		}

		/// <summary>Writes a snippet file atomically, creating its folder.</summary>
		/// <param name="path">The full path.</param>
		/// <param name="file">The content.</param>
		public static void Write(String path, SnippetFile file)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			String? directory = Path.GetDirectoryName(path);
			if (!String.IsNullOrEmpty(directory))
			{
				_ = Directory.CreateDirectory(directory);
			}

			AtomicFile.WriteAllBytes(path, Serialize(file));
		}

		private static Byte[] ToUtf8(Byte[] content)
		{
			if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
			{
				return content.AsSpan(3).ToArray();
			}

			if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
			{
				return Encoding.UTF8.GetBytes(Encoding.Unicode.GetString(content, 2, content.Length - 2));
			}

			if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
			{
				return Encoding.UTF8.GetBytes(Encoding.BigEndianUnicode.GetString(content, 2, content.Length - 2));
			}

			return content;
		}

		private static void KeepCorruptCopy(String path)
		{
			try
			{
				File.Copy(path, path + ".corrupt", true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The copy is a courtesy: the damaged file itself stays where it is.
			}
		}
	}
}