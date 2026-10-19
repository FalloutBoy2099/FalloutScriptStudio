namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;
	using IO;

	/// <summary>
	/// The signatures of the engine and sfall functions, read from the two description files of the editor. The checks of
	/// <see cref="ScriptDiagnostics"/> that compare a call with a description rely on these assumptions, and every reader is tolerant: a
	/// line it does not understand is ignored, an entry without a signature just does not give any check.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>opcodes.txt</c>: <c>ReturnType name (type name, [type name], ...) | description | more text</c>. Square brackets mark optional
	/// parameters, <c>...</c> stands for any number of further arguments, a name with no parentheses (or with empty ones) takes no
	/// argument and is written without parentheses in a script. Two words before the name of a parameter are one type
	/// (<c>unsigned int var_index</c>). A description that contains the word <c>deprecated</c> or <c>obsolete</c> marks the function
	/// as deprecated.
	/// </para>
	/// <para>
	/// <c>functions.ini</c>: <c>label&lt;d&gt;[ReturnType] description&lt;s&gt;name({type name}, {type name})</c>. The placeholders in braces are
	/// the parameters (<c>{int time}</c>, <c>{float}</c> when only the type is stated); a snippet that is just a name (<c>get_year</c>)
	/// takes no argument; a snippet that does not look like a call (<c>#define ...</c>, statements, text) gives no signature.
	/// </para>
	/// <para>
	/// A name may have a signature in both files; a call is accepted when any of them accepts it, so a mistake in one file does not
	/// produce a false finding. The names of both files also count as known names (they are never reported as undefined).
	/// </para>
	/// </remarks>
	public sealed class FunctionSignatureCatalog
	{
		private static readonly Regex _opcodeLine = new Regex(@"^(?<ret>[\w\|]+\*?)\s+(?<name>\w+)\s*(?:\((?<args>[^)]*)\))?\s*(?:\||$)", RegexOptions.CultureInvariant);
		private static readonly Regex _iniCall = new Regex(@"^\s*(?<name>[A-Za-z_]\w*)\s*(?:\((?<args>.*)\))?\s*;?\s*$", RegexOptions.CultureInvariant);
		private static readonly Regex _placeholder = new Regex(@"\{(?<text>[^}]*)\}", RegexOptions.CultureInvariant);
		private static readonly Regex _deprecated = new Regex(@"\b(?:deprecated|obsolete)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		private static readonly HashSet<String> _typeWords = new(StringComparer.OrdinalIgnoreCase)
		{
			"int", "float", "string", "objectptr", "boolean", "bool", "char", "any", "array", "procedure", "void", "unsigned", "long", "short", "byte", "dword",
		};

		private readonly Dictionary<String, FunctionSignature[]> _signatures;
		private readonly HashSet<String> _names;
		private readonly Dictionary<String, FunctionSignature[]>.AlternateLookup<ReadOnlySpan<Char>> _lookup;

		private FunctionSignatureCatalog(Dictionary<String, FunctionSignature[]> signatures, HashSet<String> names)
		{
			this._signatures = signatures;
			this._names = names;
			this._lookup = signatures.GetAlternateLookup<ReadOnlySpan<Char>>();
		}

		/// <summary>Gets a catalog with no function.</summary>
		public static FunctionSignatureCatalog Empty { get; } = new FunctionSignatureCatalog(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));

		/// <summary>Gets the names of the functions that have a signature (case is ignored when names are compared).</summary>
		public IEnumerable<String> Names => this._names;

		/// <summary>Gets the number of known names.</summary>
		public Int32 Count => this._names.Count;

		/// <summary>Creates a catalog from the lines of the two description files.</summary>
		/// <param name="opcodeLines">The lines of <c>opcodes.txt</c>.</param>
		/// <param name="functionLines">The lines of <c>functions.ini</c> (and of <c>userfunctions.ini</c>).</param>
		/// <returns>The catalog.</returns>
		public static FunctionSignatureCatalog Create(IEnumerable<String> opcodeLines, IEnumerable<String> functionLines)
		{
			ArgumentNullException.ThrowIfNull(opcodeLines);
			ArgumentNullException.ThrowIfNull(functionLines);

			Dictionary<String, List<FunctionSignature>> signatures = new(StringComparer.OrdinalIgnoreCase);
			HashSet<String> names = new(StringComparer.OrdinalIgnoreCase);

			foreach (String line in opcodeLines)
			{
				if (TryParseOpcodeLine(line, out FunctionSignature? signature) && signature is not null)
				{
					Add(signatures, names, signature);
				}
			}

			foreach (String line in functionLines)
			{
				if (TryParseIniLine(line, out String? label, out FunctionSignature? signature))
				{
					if (label is not null)
					{
						names.Add(label);
					}

					if (signature is not null)
					{
						Add(signatures, names, signature);
					}
				}
			}

			Dictionary<String, FunctionSignature[]> result = new Dictionary<String, FunctionSignature[]>(signatures.Count, StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<String, List<FunctionSignature>> entry in signatures)
			{
				result[entry.Key] = entry.Value.ToArray();
			}

			return new FunctionSignatureCatalog(result, names);
		}

		/// <summary>Loads the catalog from the two files; a missing file just gives fewer functions.</summary>
		/// <param name="opcodesPath">The path of <c>opcodes.txt</c>, or <see langword="null"/>.</param>
		/// <param name="functionPaths">The paths of <c>functions.ini</c> and <c>userfunctions.ini</c>.</param>
		/// <returns>The catalog.</returns>
		public static FunctionSignatureCatalog LoadFromFiles(String? opcodesPath, params String?[] functionPaths)
		{
			ArgumentNullException.ThrowIfNull(functionPaths);

			List<String> opcodes = ReadLines(opcodesPath);
			List<String> functions = [];
			foreach (String? path in functionPaths)
			{
				functions.AddRange(ReadLines(path));
			}

			return Create(opcodes, functions);
		}

		/// <summary>Determines whether a name belongs to a function of the description files.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <returns><see langword="true"/> when the name is in the catalog.</returns>
		public Boolean Contains(ReadOnlySpan<Char> name)
		{
			return this._lookup.ContainsKey(name) || this._names.GetAlternateLookup<ReadOnlySpan<Char>>().Contains(name);
		}

		/// <summary>Gets the signatures of a function.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <param name="signatures">Receives the signatures (several when both files describe the function).</param>
		/// <returns><see langword="true"/> when at least one signature is known.</returns>
		public Boolean TryGetSignatures(ReadOnlySpan<Char> name, out FunctionSignature[] signatures)
		{
			if (this._lookup.TryGetValue(name, out FunctionSignature[]? found))
			{
				signatures = found;
				return true;
			}

			signatures = Array.Empty<FunctionSignature>();
			return false;
		}

		private static List<String> ReadLines(String? path)
		{
			List<String> lines = [];
			if (String.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return lines;
			}

			try
			{
				String text = Decode(File.ReadAllBytes(path));
				lines.AddRange(text.Split('\n'));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A description file that cannot be read gives fewer checks, never a failure.
			}

			return lines;
		}

		private static String Decode(Byte[] bytes)
		{
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
			}

			try
			{
				return new UTF8Encoding(false, true).GetString(bytes);
			}
			catch (DecoderFallbackException)
			{
				return TextEncodings.Ansi.GetString(bytes);
			}
		}

		private static void Add(Dictionary<String, List<FunctionSignature>> signatures, HashSet<String> names, FunctionSignature signature)
		{
			names.Add(signature.Name);
			if (!signatures.TryGetValue(signature.Name, out List<FunctionSignature>? list))
			{
				list = [];
				signatures[signature.Name] = list;
			}

			list.Add(signature);
		}

		private static Boolean TryParseOpcodeLine(String line, out FunctionSignature? signature)
		{
			signature = null;
			String trimmed = line.TrimEnd('\r').Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
			{
				return false;
			}

			Match match = _opcodeLine.Match(trimmed);
			if (!match.Success)
			{
				return false;
			}

			String returnType = match.Groups["ret"].Value;
			if (String.Equals(returnType, "Handler", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			Int32 bar = trimmed.IndexOf('|', StringComparison.Ordinal);
			Boolean deprecated = bar >= 0 && _deprecated.IsMatch(trimmed.AsSpan(bar).ToString());
			List<FunctionParameter> parameters = match.Groups["args"].Success ? ParseParameters(match.Groups["args"].Value) : [];
			signature = new FunctionSignature(match.Groups["name"].Value, returnType, parameters, deprecated);
			return true;
		}

		private static List<FunctionParameter> ParseParameters(String text)
		{
			List<FunctionParameter> parameters = [];
			Boolean optional = false;
			foreach (String raw in text.Split(','))
			{
				String part = raw.Trim();
				if (part.Length == 0)
				{
					continue;
				}

				if (part.StartsWith('['))
				{
					optional = true;
					part = part.Substring(1).Trim();
				}

				Boolean closes = part.EndsWith(']');
				if (closes)
				{
					part = part.Substring(0, part.Length - 1).Trim();
				}

				if (part == "...")
				{
					parameters.Add(new FunctionParameter(String.Empty, String.Empty, true, true));
				}
				else if (part.Length > 0)
				{
					parameters.Add(ParseParameter(part, optional));
				}

				if (closes)
				{
					optional = false;
				}
			}

			return parameters;
		}

		private static FunctionParameter ParseParameter(String text, Boolean optional)
		{
			String[] words = text.Split((Char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			if (words.Length == 1)
			{
				return _typeWords.Contains(words[0]) ? new FunctionParameter(words[0], String.Empty, optional, false) : new FunctionParameter(String.Empty, words[0], optional, false);
			}

			String type = String.Join(' ', words, 0, words.Length - 1);
			return new FunctionParameter(type, words[^1], optional, false);
		}

		private static Boolean TryParseIniLine(String line, out String? label, out FunctionSignature? signature)
		{
			label = null;
			signature = null;
			String trimmed = line.TrimEnd('\r').Trim();
			Int32 description = trimmed.IndexOf("<d>", StringComparison.Ordinal);
			if (description <= 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed[0] == '<')
			{
				return false;
			}

			String name = trimmed.Substring(0, description).Trim();
			Int32 codeStart = trimmed.IndexOf("<s>", description, StringComparison.Ordinal);
			if (codeStart < 0)
			{
				return false;
			}

			String descriptionText = trimmed.Substring(description + 3, codeStart - description - 3).Trim();
			String code = trimmed.Substring(codeStart + 3);
			Int32 lineBreak = code.IndexOf("<cr>", StringComparison.Ordinal);
			if (lineBreak >= 0)
			{
				code = code.Substring(0, lineBreak);
			}

			if (IsIdentifier(name))
			{
				label = name;
			}

			Match call = _iniCall.Match(code);
			if (!call.Success)
			{
				return true;
			}

			String returnType = String.Empty;
			if (descriptionText.StartsWith('['))
			{
				Int32 close = descriptionText.IndexOf(']', StringComparison.Ordinal);
				if (close > 0)
				{
					returnType = descriptionText.Substring(1, close - 1).Trim();
				}
			}

			List<FunctionParameter> parameters = [];
			if (call.Groups["args"].Success)
			{
				String args = call.Groups["args"].Value.Trim();
				if (args.Length > 0)
				{
					MatchCollection placeholders = _placeholder.Matches(args);
					if (placeholders.Count == 0)
					{
						// Real arguments or free text instead of placeholders: nothing reliable to compare a call with.
						return true;
					}

					foreach (Match placeholder in placeholders)
					{
						String text = placeholder.Groups["text"].Value.Trim();
						if (text == "...")
						{
							parameters.Add(new FunctionParameter(String.Empty, String.Empty, true, true));
						}
						else if (text.Length > 0)
						{
							parameters.Add(ParseParameter(text, false));
						}
					}
				}
			}

			Boolean deprecated = _deprecated.IsMatch(descriptionText);
			signature = new FunctionSignature(call.Groups["name"].Value, returnType, parameters, deprecated);
			return true;
		}

		private static Boolean IsIdentifier(String text)
		{
			if (text.Length == 0 || !(text[0] == '_' || Char.IsLetter(text[0])))
			{
				return false;
			}

			foreach (Char c in text)
			{
				if (!(c == '_' || Char.IsLetterOrDigit(c)))
				{
					return false;
				}
			}

			return true;
		}
	}
}