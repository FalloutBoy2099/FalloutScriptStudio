namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.Core.IO;

	/// <summary>
	/// Reads and writes the files of the script registration: <c>scripts.lst</c> (script ids), <c>scrname.msg</c>
	/// (in-game script names) and the <c>#define</c> lines of <c>scripts.h</c>. The line formats are those of the legacy editor.
	/// Stateless and free of any UI.
	/// </summary>
	public sealed class ScriptRegistryStore
	{
		/// <summary>The comment written at the top of <c>scrname.msg</c>.</summary>
		public const String MessageFileHeader = "#\r\n#   This file was built using Sfall Script Editor.\r\n#";

		/// <summary>The prefix of the script constants in <c>scripts.h</c>.</summary>
		public const String DefinePrefix = "SCRIPT_";

		private const Int32 DefineNameColumnWidth = 32;
		private const Int32 DefineValueColumnWidth = 8;
		private const Int32 DefineScriptColumnWidth = 16;

		/// <summary>
		/// Loads <c>scripts.lst</c> and, when available, the script names from <c>scrname.msg</c>.
		/// </summary>
		/// <param name="paths">The files.</param>
		/// <param name="messageEncoding">The encoding of <c>scrname.msg</c>.</param>
		/// <returns>The registry.</returns>
		/// <exception cref="IOException">A file could not be read.</exception>
		public ScriptRegistry Load(ScriptRegistryPaths paths, Encoding messageEncoding)
		{
			ArgumentNullException.ThrowIfNull(paths);
			ArgumentNullException.ThrowIfNull(messageEncoding);

			ScriptTextFile.DecodedText list = ScriptTextFile.Decode(File.ReadAllBytes(paths.ListPath), TextEncodings.Ansi);
			ScriptRegistry registry = new ScriptRegistry(list.Encoding);
			foreach (String line in ScriptTextFile.SplitLines(list.Text))
			{
				registry.Entries.Add(ScriptListEntry.Parse(line));
			}

			if (paths.NameMessagePath is not null)
			{
				ApplyGameNames(registry.Entries, File.ReadAllLines(paths.NameMessagePath, messageEncoding));
			}

			return registry;
		}

		/// <summary>
		/// Copies the in-game names from the lines of <c>scrname.msg</c> to the entries: message <c>101</c> belongs to the first script.
		/// Lines that are not messages, or that refer to a missing script, are ignored.
		/// </summary>
		/// <param name="entries">The entries.</param>
		/// <param name="messageLines">The lines of the message file.</param>
		public static void ApplyGameNames(IReadOnlyList<ScriptListEntry> entries, IEnumerable<String> messageLines)
		{
			ArgumentNullException.ThrowIfNull(entries);
			ArgumentNullException.ThrowIfNull(messageLines);

			foreach (String messageLine in messageLines)
			{
				String[] parts = messageLine.Split('}');
				if (parts.Length != 4)
				{
					continue;
				}

				if (!Int32.TryParse(parts[0].TrimStart(' ', '{'), NumberStyles.None, CultureInfo.InvariantCulture, out Int32 id))
				{
					continue;
				}

				Int32 index = id - ScriptListEntry.FirstMessageId;
				if (index < 0 || index >= entries.Count)
				{
					continue;
				}

				entries[index].Lip = parts[1].StartsWith('{') ? parts[1].Substring(1) : parts[1];
				entries[index].GameName = parts[2].StartsWith('{') ? parts[2].Substring(1) : parts[2];
			}
		}

		/// <summary>
		/// Writes <c>scripts.lst</c> and, when a path is given, <c>scrname.msg</c>.
		/// </summary>
		/// <param name="registry">The registry.</param>
		/// <param name="paths">The files.</param>
		/// <param name="messageEncoding">The encoding of <c>scrname.msg</c>.</param>
		/// <exception cref="IOException">A file could not be written.</exception>
		public void Save(ScriptRegistry registry, ScriptRegistryPaths paths, Encoding messageEncoding)
		{
			ArgumentNullException.ThrowIfNull(registry);
			ArgumentNullException.ThrowIfNull(paths);
			ArgumentNullException.ThrowIfNull(messageEncoding);

			List<String> lines = new List<String>(registry.Entries.Count);
			foreach (ScriptListEntry entry in registry.Entries)
			{
				lines.Add(entry.ToListLine());
			}

			ScriptTextFile.WriteAllBytesAtomic(paths.ListPath, ScriptTextFile.Encode(ScriptTextFile.JoinLines(lines), registry.ListEncoding, false));

			if (paths.NameMessagePath is not null)
			{
				Boolean replaceCyrillicHa = messageEncoding.CodePage == 866;
				List<String> messageLines = new List<String> { MessageFileHeader };
				for (Int32 i = 0; i < registry.Entries.Count; i++)
				{
					messageLines.Add(registry.Entries[i].ToMessageLine(i, replaceCyrillicHa));
				}

				ScriptTextFile.WriteAllBytesAtomic(paths.NameMessagePath, ScriptTextFile.Encode(ScriptTextFile.JoinLines(messageLines), messageEncoding, false));
			}
		}

		/// <summary>
		/// Formats the <c>#define</c> line of a script (<c>#define SCRIPT_NAME   (id)   // file.int ; description</c>).
		/// </summary>
		/// <param name="defineName">The constant name.</param>
		/// <param name="scriptNumber">The script id (1-based).</param>
		/// <param name="entry">The script.</param>
		/// <returns>The line.</returns>
		public static String FormatDefine(String defineName, Int32 scriptNumber, ScriptListEntry entry)
		{
			ArgumentNullException.ThrowIfNull(defineName);
			ArgumentNullException.ThrowIfNull(entry);

			return ("#define " + defineName.ToUpperInvariant()).PadRight(DefineNameColumnWidth)
				+ ("(" + scriptNumber.ToString(CultureInfo.InvariantCulture) + ")").PadRight(DefineValueColumnWidth)
				+ "// " + entry.Script.PadRight(DefineScriptColumnWidth) + "; " + entry.Description;
		}

		/// <summary>
		/// Adds the definition of a script to the header file, in front of the last line that contains a <c>#</c> (normally <c>#endif</c>).
		/// </summary>
		/// <param name="headerPath">The <c>scripts.h</c> file.</param>
		/// <param name="defineName">The constant name.</param>
		/// <param name="scriptNumber">The script id (1-based).</param>
		/// <param name="entry">The script.</param>
		/// <returns>Whether the line was added.</returns>
		/// <exception cref="IOException">The file could not be read or written.</exception>
		public DefineInsertResult AddDefine(String headerPath, String defineName, Int32 scriptNumber, ScriptListEntry entry)
		{
			ArgumentException.ThrowIfNullOrEmpty(headerPath);
			ArgumentException.ThrowIfNullOrEmpty(defineName);
			ArgumentNullException.ThrowIfNull(entry);

			ScriptTextFile.DecodedText header = ScriptTextFile.Decode(File.ReadAllBytes(headerPath), TextEncodings.Ansi);
			List<String> lines = ScriptTextFile.SplitLines(header.Text);
			if (InsertDefine(lines, defineName, scriptNumber, entry) == DefineInsertResult.AlreadyDefined)
			{
				return DefineInsertResult.AlreadyDefined;
			}

			ScriptTextFile.WriteAllBytesAtomic(headerPath, ScriptTextFile.Encode(ScriptTextFile.JoinLines(lines), header.Encoding, header.HasByteOrderMark));
			return DefineInsertResult.Added;
		}

		/// <summary>
		/// Inserts the definition line into the lines of a header file.
		/// </summary>
		/// <param name="lines">The lines of the header; modified when the definition is added.</param>
		/// <param name="defineName">The constant name.</param>
		/// <param name="scriptNumber">The script id (1-based).</param>
		/// <param name="entry">The script.</param>
		/// <returns>Whether the line was added.</returns>
		public static DefineInsertResult InsertDefine(List<String> lines, String defineName, Int32 scriptNumber, ScriptListEntry entry)
		{
			ArgumentNullException.ThrowIfNull(lines);

			String name = defineName.ToUpperInvariant();
			foreach (String line in lines)
			{
				String[] tokens = line.Split((Char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length >= 2 && String.Equals(tokens[0], "#define", StringComparison.Ordinal) && String.Equals(tokens[1], name, StringComparison.OrdinalIgnoreCase))
				{
					return DefineInsertResult.AlreadyDefined;
				}
			}

			String define = FormatDefine(name, scriptNumber, entry);
			for (Int32 i = lines.Count - 1; i >= 0; i--)
			{
				if (lines[i].Contains('#') || i == 0)
				{
					lines.Insert(i, define);
					return DefineInsertResult.Added;
				}
			}

			lines.Add(define);
			return DefineInsertResult.Added;
		}
	}
}
