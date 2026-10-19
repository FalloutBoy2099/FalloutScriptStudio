namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Functions;

	/// <summary>
	/// Turns the code templates of the functions tree (<c>userfunctions.ini</c>: <c>name&lt;d&gt;description&lt;s&gt;code</c> with <c>{placeholder}</c>
	/// markers and <c>&lt;cr&gt;</c> line breaks) into snippets, so that the templates a user wrote for the legacy editor are not lost.
	/// </summary>
	public static class SnippetImporter
	{
		/// <summary>Converts the code of a function into the body of a snippet.</summary>
		/// <param name="code">The code as stored in the ini file.</param>
		/// <returns>The body: <c>{name}</c> became <c>${1:name}</c> (the same name is the same stop, so it is mirrored), <c>&lt;cr&gt;</c> a line break.</returns>
		public static String ConvertCode(String code)
		{
			ArgumentNullException.ThrowIfNull(code);

			StringBuilder body = new StringBuilder(code.Length + 16);
			Dictionary<String, Int32> numbers = new(StringComparer.Ordinal);
			Int32 i = 0;
			while (i < code.Length)
			{
				if (String.CompareOrdinal(code, i, FunctionCatalogParser.LineBreakMarker, 0, FunctionCatalogParser.LineBreakMarker.Length) == 0)
				{
					body.Append('\n');
					i += FunctionCatalogParser.LineBreakMarker.Length;
					continue;
				}

				Char c = code[i];
				if (c == '{')
				{
					Int32 close = code.IndexOf('}', i + 1);
					if (close > i)
					{
						String name = code.Substring(i + 1, close - i - 1);
						if (!name.Contains(FunctionCatalogParser.LineBreakMarker, StringComparison.Ordinal))
						{
							if (!numbers.TryGetValue(name, out Int32 number))
							{
								number = numbers.Count + 1;
								numbers[name] = number;
							}

							body.Append("${").Append(number);
							if (name.Length > 0)
							{
								body.Append(':').Append(Escape(name));
							}

							body.Append('}');
							i = close + 1;
							continue;
						}
					}
				}

				body.Append(c is '$' or '\\' ? "\\" + c : c.ToString());
				i++;
			}

			return body.ToString();
		}

		/// <summary>Creates the snippet of a function.</summary>
		/// <param name="name">The name in the tree.</param>
		/// <param name="description">The description.</param>
		/// <param name="code">The code as stored in the ini file.</param>
		/// <param name="usedTriggers">The triggers that are taken already (the trigger made for this snippet is not one of them).</param>
		/// <returns>The snippet, a user snippet.</returns>
		public static Snippet FromFunction(String name, String description, String code, IEnumerable<String> usedTriggers)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(description);
			ArgumentNullException.ThrowIfNull(code);
			ArgumentNullException.ThrowIfNull(usedTriggers);

			return new Snippet(MakeTrigger(name, usedTriggers), description.Length > 0 ? description : name, ConvertCode(code)) { Source = SnippetSource.User };
		}

		/// <summary>Creates the snippets of the functions of a tree.</summary>
		/// <param name="nodes">The nodes (categories are followed, separators are ignored).</param>
		/// <param name="usedTriggers">The triggers that are taken already; the triggers made are added to the list.</param>
		/// <returns>One snippet per function that has code.</returns>
		public static IReadOnlyList<Snippet> FromNodes(IEnumerable<FunctionNode> nodes, ICollection<String> usedTriggers)
		{
			ArgumentNullException.ThrowIfNull(nodes);
			ArgumentNullException.ThrowIfNull(usedTriggers);

			List<Snippet> snippets = [];
			Collect(nodes, usedTriggers, snippets);
			return snippets;
		}

		/// <summary>Makes a trigger from a name: letters, digits and underscores of the name in lower case, never a reserved word or a trigger in use.</summary>
		/// <param name="name">The name.</param>
		/// <param name="usedTriggers">The triggers in use.</param>
		/// <returns>A valid trigger.</returns>
		public static String MakeTrigger(String name, IEnumerable<String> usedTriggers)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(usedTriggers);

			StringBuilder builder = new StringBuilder();
			foreach (Char c in name)
			{
				if (builder.Length < SnippetValidator.MaxTriggerLength - 4 && (Char.IsAsciiLetterOrDigit(c) || c == '_'))
				{
					builder.Append(Char.ToLowerInvariant(c));
				}
			}

			if (builder.Length == 0 || Char.IsDigit(builder[0]))
			{
				builder.Insert(0, 'f');
			}

			if (SnippetValidator.IsReserved(builder.ToString()))
			{
				builder.Append('x');
			}

			HashSet<String> used = new HashSet<String>(usedTriggers, StringComparer.OrdinalIgnoreCase);
			String baseTrigger = builder.ToString();
			String trigger = baseTrigger;
			for (Int32 number = 2; used.Contains(trigger); number++)
			{
				trigger = baseTrigger + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
			}

			return trigger;
		}

		private static void Collect(IEnumerable<FunctionNode> nodes, ICollection<String> usedTriggers, List<Snippet> snippets)
		{
			foreach (FunctionNode node in nodes)
			{
				if (node.Kind == FunctionNodeKind.Function && node.Code.Trim().Length > 0)
				{
					Snippet snippet = FromFunction(node.Name, node.Description, node.Code, usedTriggers);
					usedTriggers.Add(snippet.Trigger);
					snippets.Add(snippet);
				}

				Collect(node.Children, usedTriggers, snippets);
			}
		}

		private static String Escape(String text)
		{
			StringBuilder builder = new StringBuilder(text.Length);
			foreach (Char c in text)
			{
				if (c is '$' or '\\' or '}')
				{
					builder.Append('\\');
				}

				builder.Append(c);
			}

			return builder.ToString();
		}
	}
}