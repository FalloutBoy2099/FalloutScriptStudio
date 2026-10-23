namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using IO;

	/// <summary>
	/// The language folders of the game (<c>text\english\dialog\x.msg</c>, <c>text\french\dialog\x.msg</c>...): the names the game and its translations use,
	/// the code page each language is written in, and the files of the other languages that belong to a message file.
	/// </summary>
	public static class MessageLanguages
	{
		/// <summary>The legacy code page of the DOS Cyrillic option of the settings (cp866).</summary>
		public const Int32 DosCyrillicCodePage = 866;

		private const String TextFolder = "text";
		private const Int32 WesternCodePage = 1252;
		private const Int32 CyrillicCodePage = 1251;

		private static readonly Dictionary<String, Int32> _codePages = new(StringComparer.OrdinalIgnoreCase)
		{
			["english"] = WesternCodePage,
			["french"] = WesternCodePage,
			["german"] = WesternCodePage,
			["spanish"] = WesternCodePage,
			["italian"] = WesternCodePage,
			["portuguese"] = WesternCodePage,
			["brazilian"] = WesternCodePage,
			["dutch"] = WesternCodePage,
			["swedish"] = WesternCodePage,
			["danish"] = WesternCodePage,
			["norwegian"] = WesternCodePage,
			["finnish"] = WesternCodePage,
			["polish"] = 1250,
			["czech"] = 1250,
			["slovak"] = 1250,
			["hungarian"] = 1250,
			["romanian"] = 1250,
			["russian"] = CyrillicCodePage,
			["ukrainian"] = CyrillicCodePage,
			["bulgarian"] = CyrillicCodePage,
			["greek"] = 1253,
			["turkish"] = 1254,
			["chinese"] = 936,
			["japanese"] = 932,
			["korean"] = 949,
		};

		/// <summary>Gets the names of the language folders the editor knows, in alphabetical order.</summary>
		public static IReadOnlyList<String> KnownFolders { get; } = _codePages.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();

		/// <summary>Determines whether a folder name is the name of a language of the game.</summary>
		/// <param name="folder">The name of the folder.</param>
		/// <returns><see langword="true"/> for <c>english</c>, <c>french</c>, <c>russian</c>... (the case is ignored).</returns>
		public static Boolean IsKnown(String folder)
		{
			ArgumentNullException.ThrowIfNull(folder);
			return _codePages.ContainsKey(folder);
		}

		/// <summary>Finds the language a message file belongs to: the folder below <c>text</c> in its path, otherwise the last folder of the path that names a language.</summary>
		/// <param name="path">The path of the message file.</param>
		/// <returns>The name of the language folder as written in the path, or <see langword="null"/>.</returns>
		public static String? DetectLanguage(String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			String[] segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
			for (Int32 i = segments.Length - 3; i >= 0; i--)
			{
				if (String.Equals(segments[i], TextFolder, StringComparison.OrdinalIgnoreCase))
				{
					return segments[i + 1];
				}
			}

			for (Int32 i = segments.Length - 2; i >= 0; i--)
			{
				if (IsKnown(segments[i]))
				{
					return segments[i];
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the encoding a language is written in. The table gives the code page of every language it knows (Windows-1252 for the western
		/// languages, Windows-1251 for Russian...); the legacy option "DOS Cyrillic" (cp866) of the settings wins for the Cyrillic languages, and the
		/// encoding of the settings is used for a language the table does not know.
		/// </summary>
		/// <param name="language">The language folder, or <see langword="null"/> when the path names none.</param>
		/// <param name="configured">The encoding of the settings (<c>Editor.FileEncoding</c>).</param>
		/// <returns>The encoding.</returns>
		public static Encoding GetEncoding(String? language, Encoding configured)
		{
			ArgumentNullException.ThrowIfNull(configured);

			if (language is null || !_codePages.TryGetValue(language, out Int32 codePage))
			{
				return configured;
			}

			if (codePage == CyrillicCodePage && configured.CodePage == DosCyrillicCodePage)
			{
				return configured;
			}

			return codePage == configured.CodePage ? configured : TextEncodings.FromCodePage(codePage);
		}

		/// <summary>Finds the files of the other languages that belong to a message file: for <c>text\english\dialog\x.msg</c> the files <c>text\&lt;language&gt;\dialog\x.msg</c> that exist.</summary>
		/// <param name="path">The path of the message file.</param>
		/// <returns>The languages and paths, in alphabetical order of the language; empty when the path is not below a <c>text\&lt;language&gt;</c> folder.</returns>
		public static IReadOnlyList<MessageLanguageFile> FindSiblings(String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			String full = Path.GetFullPath(path);
			List<String> relative = [Path.GetFileName(full)];
			DirectoryInfo? folder = new FileInfo(full).Directory;
			DirectoryInfo? textFolder = null;
			while (folder?.Parent is not null)
			{
				if (String.Equals(folder.Parent.Name, TextFolder, StringComparison.OrdinalIgnoreCase))
				{
					textFolder = folder.Parent;
					break;
				}

				relative.Insert(0, folder.Name);
				folder = folder.Parent;
			}

			if (textFolder is null)
			{
				return Array.Empty<MessageLanguageFile>();
			}

			String relativePath = Path.Combine(relative.ToArray());
			List<MessageLanguageFile> found = [];
			try
			{
				foreach (DirectoryInfo language in textFolder.EnumerateDirectories().OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase))
				{
					String candidate = Path.Combine(language.FullName, relativePath);
					if (!String.Equals(candidate, full, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
					{
						found.Add(new MessageLanguageFile(language.Name, candidate));
					}
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Array.Empty<MessageLanguageFile>();
			}

			return found;
		}
	}
}