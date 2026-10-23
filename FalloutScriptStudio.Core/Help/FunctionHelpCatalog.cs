namespace FalloutScriptStudio.Core.Help
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Analysis;
	using Functions;
	using TextTools;

	/// <summary>
	/// The documentation of every built-in function, opcode, constant and user function, made from the two description files: the signature
	/// and the description come from <c>opcodes.txt</c> (<c>ReturnType name (parameters) | - description | more || note</c>, a single
	/// <c>|</c> is a line break and <c>||</c> a new paragraph), the category, the one-line description and the code to insert from the
	/// functions tree (<c>functions.ini</c> and <c>userfunctions.ini</c>). A name in both files is one topic. The catalog is immutable.
	/// </summary>
	public sealed class FunctionHelpCatalog
	{
		private static readonly Regex _header = new(@"^(?<ret>[\w\|]+\*?)\s+(?<name>\w+)", RegexOptions.CultureInvariant);
		private static readonly Regex _argument = new(@"^(?:[A-Za-z_]+\s+)?(?:arg|ret)\d+\s*-", RegexOptions.CultureInvariant);
		private static readonly Regex _defineValue = new(@"^:\s*value\s+(?<value>\S+)", RegexOptions.CultureInvariant);

		private readonly Dictionary<String, HelpTopic> _byName;
		private readonly HelpTopic[] _topics;

		private FunctionHelpCatalog(Dictionary<String, HelpTopic> byName)
		{
			this._byName = byName;
			this._topics = byName.Values.OrderBy(static t => t.Name, StringComparer.OrdinalIgnoreCase).ToArray();
		}

		/// <summary>Gets an empty catalog.</summary>
		public static FunctionHelpCatalog Empty { get; } = new FunctionHelpCatalog(new(StringComparer.OrdinalIgnoreCase));

		/// <summary>Gets the number of topics.</summary>
		public Int32 Count => this._topics.Length;

		/// <summary>Gets every topic ordered by name.</summary>
		public IReadOnlyList<HelpTopic> Topics => this._topics;

		/// <summary>Creates the catalog.</summary>
		/// <param name="opcodeLines">The lines of <c>opcodes.txt</c>.</param>
		/// <param name="tree">The functions tree of <c>functions.ini</c> followed by the one of <c>userfunctions.ini</c>.</param>
		/// <returns>The catalog.</returns>
		public static FunctionHelpCatalog Create(IEnumerable<String> opcodeLines, FunctionCatalog tree)
		{
			ArgumentNullException.ThrowIfNull(opcodeLines);
			ArgumentNullException.ThrowIfNull(tree);

			Dictionary<String, Draft> drafts = new(StringComparer.OrdinalIgnoreCase);
			ReadOpcodes(opcodeLines, drafts);
			ReadTree(tree.Roots, [], drafts);

			Dictionary<String, HelpTopic> topics = new(drafts.Count, StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<String, Draft> entry in drafts)
			{
				topics.Add(entry.Key, entry.Value.ToTopic());
			}

			return new FunctionHelpCatalog(topics);
		}

		/// <summary>Finds the topic of a name.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <param name="topic">Receives the topic.</param>
		/// <returns><see langword="true"/> when the description files know the name.</returns>
		public Boolean TryGet(String name, out HelpTopic? topic)
		{
			ArgumentNullException.ThrowIfNull(name);
			return this._byName.TryGetValue(name, out topic);
		}

		/// <summary>Finds the topics that match a filter (the name; several words must all match), the best first.</summary>
		/// <param name="filter">The filter text; every topic by name when blank.</param>
		/// <param name="maxResults">The most topics to return.</param>
		/// <returns>The topics.</returns>
		public IReadOnlyList<HelpTopic> Search(String? filter, Int32 maxResults)
		{
			if (maxResults <= 0)
			{
				return Array.Empty<HelpTopic>();
			}

			if (String.IsNullOrWhiteSpace(filter))
			{
				return this._topics.Take(maxResults).ToArray();
			}

			return FuzzyMatcher.Rank(this._topics, filter, static t => t.Name).Take(maxResults).ToArray();
		}

		private static void ReadOpcodes(IEnumerable<String> lines, Dictionary<String, Draft> drafts)
		{
			Boolean sfall = false;
			foreach (String raw in lines)
			{
				String line = raw.TrimEnd('\r').Trim();
				if (line.Length == 0)
				{
					continue;
				}

				if (line.StartsWith("//", StringComparison.Ordinal))
				{
					sfall |= line.Contains("SFALL", StringComparison.Ordinal);
					continue;
				}

				Match header = _header.Match(line);
				if (!header.Success)
				{
					continue;
				}

				String kindWord = header.Groups["ret"].Value;
				String name = header.Groups["name"].Value;
				Int32 bar = line.IndexOf('|', StringComparison.Ordinal);
				String rest = bar >= 0 ? line.Substring(bar + 1) : String.Empty;
				Draft draft = new(name) { IsSfall = sfall };
				if (String.Equals(kindWord, "Handler", StringComparison.OrdinalIgnoreCase))
				{
					draft.Kind = HelpTopicKind.Handler;
					draft.Segments = [new SignatureSegment(name, SignatureSegmentKind.Name)];
				}
				else if (String.Equals(kindWord, "Define", StringComparison.OrdinalIgnoreCase))
				{
					draft.Kind = HelpTopicKind.Define;
					String afterName = (bar >= 0 ? line.Substring(0, bar) : line).Substring(header.Length).Trim();
					Match value = _defineValue.Match(afterName);
					draft.Segments = value.Success
						?
						[
							new SignatureSegment(name, SignatureSegmentKind.Name),
							new SignatureSegment(" = ", SignatureSegmentKind.Punctuation),
							new SignatureSegment(value.Groups["value"].Value, SignatureSegmentKind.ParameterName),
						]
						: [new SignatureSegment(name, SignatureSegmentKind.Name)];
				}
				else if (FunctionSignatureCatalog.TryParseOpcodeLine(line, out FunctionSignature? signature) && signature is not null)
				{
					draft.Segments = FunctionSignatureFormatter.Format(signature);
				}
				else
				{
					continue;
				}

				ParseDescription(rest, draft);

				// A name can be described twice (a macro and an opcode); the later description replaces the earlier one.
				drafts[name] = draft;
			}
		}

		private static void ParseDescription(String text, Draft draft)
		{
			List<String> paragraphs = [];
			List<String> arguments = [];
			foreach (String paragraph in text.Split("||", StringSplitOptions.None))
			{
				List<String> lines = [];
				foreach (String part in paragraph.Split('|'))
				{
					String line = part.Trim();
					if (line.StartsWith("- ", StringComparison.Ordinal))
					{
						line = line.Substring(2).TrimStart();
					}
					else if (line == "-")
					{
						line = String.Empty;
					}

					if (line.Length == 0)
					{
						continue;
					}

					if (_argument.IsMatch(line))
					{
						arguments.Add(line);
					}
					else
					{
						lines.Add(line);
					}
				}

				if (lines.Count > 0)
				{
					paragraphs.Add(String.Join('\n', lines));
				}
			}

			draft.Description = paragraphs;
			draft.Arguments = arguments;
		}

		private static void ReadTree(IReadOnlyList<FunctionNode> nodes, List<String> path, Dictionary<String, Draft> drafts)
		{
			foreach (FunctionNode node in nodes)
			{
				if (node.Kind == FunctionNodeKind.Category)
				{
					path.Add(node.Name);
					ReadTree(node.Children, path, drafts);
					path.RemoveAt(path.Count - 1);
				}
				else if (node.Kind == FunctionNodeKind.Function)
				{
					ReadFunction(node, String.Join(" > ", path), path.Count > 0 && path[0].StartsWith("sfall", StringComparison.OrdinalIgnoreCase), drafts);
				}
			}
		}

		private static void ReadFunction(FunctionNode node, String category, Boolean sfallCategory, Dictionary<String, Draft> drafts)
		{
			String label = node.Name.Trim();
			String keyword = FunctionKeyword.Extract(node.Code);
			String name = IsIdentifier(label) ? (String.Equals(label, keyword, StringComparison.OrdinalIgnoreCase) ? keyword : label) : keyword;
			if (name.Length == 0)
			{
				return;
			}

			if (!drafts.TryGetValue(name, out Draft? draft))
			{
				draft = new Draft(name) { Kind = node.IsUser ? HelpTopicKind.UserFunction : HelpTopicKind.Function };
				if (FunctionSignatureCatalog.TryParseIniLine(label + FunctionCatalogParser.DescriptionMarker + node.Description + FunctionCatalogParser.CodeMarker + node.Code, out _, out FunctionSignature? signature) && signature is not null)
				{
					draft.Segments = FunctionSignatureFormatter.Format(signature);
				}
				else
				{
					draft.Segments = [new SignatureSegment(label, SignatureSegmentKind.Name)];
				}

				drafts.Add(name, draft);
			}

			draft.IsSfall |= sfallCategory;
			if (draft.Summary.Length == 0)
			{
				draft.Summary = StripTypeTag(node.Description);
			}

			if (draft.InsertCode is null && node.Code.Length > 0)
			{
				draft.InsertCode = node.Code;
			}

			if (category.Length > 0 && !draft.Categories.Contains(category))
			{
				draft.Categories.Add(category);
			}
		}

		private static String StripTypeTag(String description)
		{
			String text = description.Trim();
			if (text.StartsWith('['))
			{
				Int32 close = text.IndexOf(']', StringComparison.Ordinal);
				if (close > 0)
				{
					text = text.Substring(close + 1).Trim();
				}
			}

			return text;
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

		private sealed class Draft
		{
			internal Draft(String name)
			{
				this.Name = name;
				this.Segments = [new SignatureSegment(name, SignatureSegmentKind.Name)];
			}

			internal String Name { get; }

			internal HelpTopicKind Kind { get; set; }

			internal Boolean IsSfall { get; set; }

			internal IReadOnlyList<SignatureSegment> Segments { get; set; }

			internal IReadOnlyList<String> Description { get; set; } = Array.Empty<String>();

			internal IReadOnlyList<String> Arguments { get; set; } = Array.Empty<String>();

			internal String Summary { get; set; } = String.Empty;

			internal String? InsertCode { get; set; }

			internal List<String> Categories { get; } = [];

			internal HelpTopic ToTopic()
			{
				String summary = this.Summary.Length > 0 ? this.Summary : this.Description.Count > 0 ? this.Description[0].Split('\n')[0] : String.Empty;
				IReadOnlyList<String> description = this.Description.Count > 0 ? this.Description : this.Summary.Length > 0 ? [this.Summary] : Array.Empty<String>();
				return new HelpTopic(this.Name, this.Kind, this.Segments)
				{
					Summary = summary,
					Description = description,
					Arguments = this.Arguments,
					Categories = this.Categories.ToArray(),
					InsertCode = this.InsertCode,
					IsSfall = this.IsSfall,
					DocumentationPage = this.IsSfall ? SfallDocumentation.FindPage(this.Name, this.Categories) : null,
				};
			}
		}
	}
}