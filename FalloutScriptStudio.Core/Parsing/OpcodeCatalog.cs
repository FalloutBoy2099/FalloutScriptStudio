namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Catalog of the built-in opcodes and their descriptions, loaded from <c>opcodes.txt</c>
	/// (legacy <c>ProgramInfo.LoadOpcodes</c>, <c>LookupOpcode</c> and <c>LookupOpcodesToken</c>).
	/// </summary>
	/// <remarks>
	/// Each line of the file has the form <c>ReturnType name (arguments) | description | more text || note</c>.
	/// A single <c>|</c> starts a new line in the description and <c>||</c> starts a new paragraph. The catalog is immutable.
	/// </remarks>
	public sealed class OpcodeCatalog
	{
		/// <summary>The default maximum length of a description line.</summary>
		public const Int32 DefaultWrapWidth = 150;

		private static readonly Regex _opcodeLine = new Regex(@"^[\w\|]+\*?\s+(\w+).*", RegexOptions.CultureInvariant);

		private readonly Dictionary<String, String> _descriptions = new(StringComparer.Ordinal);
		private readonly SortedDictionary<String, String> _namesByLowerCase = new(StringComparer.Ordinal);

		/// <summary>
		/// Initializes a new instance of the <see cref="OpcodeCatalog"/> class.
		/// </summary>
		/// <param name="lines">The lines of the opcode description file.</param>
		/// <param name="shortDescription">When <c>true</c>, only the first description line of every opcode is kept.</param>
		/// <param name="wrapWidth">Maximum length of a description line before it is wrapped.</param>
		public OpcodeCatalog(IEnumerable<String> lines, Boolean shortDescription = false, Int32 wrapWidth = DefaultWrapWidth)
		{
			foreach (String line in lines)
			{
				Match match = _opcodeLine.Match(line);
				if (match.Success)
				{
					this._descriptions[match.Groups[1].Value] = Wrap(line, shortDescription, wrapWidth);
				}
			}

			foreach (String name in this._descriptions.Keys)
			{
				this._namesByLowerCase[name.ToLowerInvariant()] = name;
			}
		}

		/// <summary>Gets the number of opcodes.</summary>
		public Int32 Count => this._descriptions.Count;

		/// <summary>
		/// Loads the catalog from a file. A missing file produces an empty catalog.
		/// </summary>
		/// <param name="path">Path of <c>opcodes.txt</c>.</param>
		/// <param name="shortDescription">When <c>true</c>, only the first description line of every opcode is kept.</param>
		/// <param name="wrapWidth">Maximum length of a description line before it is wrapped.</param>
		/// <returns>The catalog.</returns>
		public static OpcodeCatalog LoadFromFile(String path, Boolean shortDescription = false, Int32 wrapWidth = DefaultWrapWidth)
		{
			if (!File.Exists(path))
			{
				return new OpcodeCatalog(Array.Empty<String>(), shortDescription, wrapWidth);
			}

			return new OpcodeCatalog(SslSyntax.SplitLines(TextFileReader.ReadAllText(path)), shortDescription, wrapWidth);
		}

		/// <summary>Gets the description of an opcode.</summary>
		/// <param name="token">The opcode name (case-insensitive).</param>
		/// <returns>The description, or <c>null</c> when the token is not an opcode.</returns>
		public String? LookupToken(String token)
		{
			return this._namesByLowerCase.TryGetValue(token.ToLowerInvariant(), out String? name)
				? this._descriptions[name]
				: null;
		}

		/// <summary>Finds the opcodes whose name starts with the given text.</summary>
		/// <param name="prefix">The beginning of the name (case-insensitive).</param>
		/// <returns>The matching opcodes ordered by name.</returns>
		public IReadOnlyList<AutosuggestItem> Lookup(String prefix)
		{
			var matches = new List<AutosuggestItem>();
			foreach (KeyValuePair<String, String> entry in this._namesByLowerCase)
			{
				if (entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					matches.Add(new AutosuggestItem(entry.Value, this._descriptions[entry.Value], AutosuggestKind.Opcode));
				}
			}

			return matches;
		}

		private static String Wrap(String line, Boolean shortDescription, Int32 wrapWidth)
		{
			var wrapped = new StringBuilder();
			Int32 lineLength = 0;
			Int32 separators = 0;

			foreach (String word in line.Split(' '))
			{
				Boolean isSeparator = word == "|" || word == "||";
				if (lineLength + word.Length > wrapWidth || isSeparator)
				{
					wrapped.Append(word == "||" ? "\n\n" : "\n");
					lineLength = 0;
					if (isSeparator)
					{
						if (shortDescription && ++separators > 1)
						{
							break;
						}

						continue;
					}
				}

				if (wrapped.Length > 0)
				{
					wrapped.Append(' ');
				}

				wrapped.Append(word);
				lineLength += word.Length + 1;
			}

			return wrapped.ToString().TrimEnd('\n');
		}
	}
}