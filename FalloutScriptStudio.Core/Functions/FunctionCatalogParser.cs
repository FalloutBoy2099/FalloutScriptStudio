namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Reads the format of <c>functions.ini</c> and <c>userfunctions.ini</c> (legacy <c>Functions.BuildFunctionTree</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// One item per line; lines are trimmed, blank lines and lines starting with <c>//</c> are ignored.
	/// <c>&lt;m&gt;title</c> starts a top level category, <c>&lt;m+&gt;title</c> a nested one, <c>&lt;m-&gt;</c> closes the innermost
	/// nested category, <c>&lt;-&gt;</c> is a separator between top level categories, and
	/// <c>name&lt;d&gt;description&lt;s&gt;code</c> is a function in the innermost open category. <c>&lt;cr&gt;</c> in the code stands for a line break.
	/// </para>
	/// <para>
	/// Unlike the legacy reader, malformed lines never throw: a function line without <c>&lt;s&gt;</c> becomes a function with
	/// empty code, and lines outside any category, unmatched <c>&lt;m-&gt;</c> and unknown lines are reported in
	/// <see cref="ParsedFunctionFile.Issues"/> and skipped. Categories can be nested to any depth.
	/// </para>
	/// </remarks>
	public static class FunctionCatalogParser
	{
		/// <summary>The marker that separates the function name from its description.</summary>
		public const String DescriptionMarker = "<d>";

		/// <summary>The marker that separates the description from the code.</summary>
		public const String CodeMarker = "<s>";

		/// <summary>The marker for a line break inside the code.</summary>
		public const String LineBreakMarker = "<cr>";

		/// <summary>The marker of a top level category.</summary>
		public const String CategoryMarker = "<m>";

		/// <summary>The marker of a nested category.</summary>
		public const String SubCategoryMarker = "<m+>";

		/// <summary>The marker that closes a nested category.</summary>
		public const String EndCategoryMarker = "<m->";

		/// <summary>The line that is a separator.</summary>
		public const String SeparatorMarker = "<->";

		/// <summary>Gets the name given to separator nodes (50 dashes, as the legacy tree showed them).</summary>
		public static String SeparatorName { get; } = new String('-', 50);

		/// <summary>Parses the lines of an ini file.</summary>
		/// <param name="lines">The lines of the file.</param>
		/// <param name="source">Which file the lines come from.</param>
		/// <returns>The top level nodes and the problems found.</returns>
		public static ParsedFunctionFile Parse(IReadOnlyList<String> lines, FunctionSource source)
		{
			ArgumentNullException.ThrowIfNull(lines);

			var roots = new List<Builder>();
			var issues = new List<FunctionCatalogIssue>();
			var open = new List<Builder>();

			for (Int32 i = 0; i < lines.Count; i++)
			{
				String line = lines[i].Trim();
				if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
				{
					continue;
				}

				if (line.StartsWith(CategoryMarker, StringComparison.Ordinal))
				{
					var category = new Builder(FunctionNodeKind.Category, line.Substring(CategoryMarker.Length), 0, i, line);
					roots.Add(category);
					open.Clear();
					open.Add(category);
				}
				else if (line.StartsWith(SubCategoryMarker, StringComparison.Ordinal))
				{
					if (open.Count == 0)
					{
						issues.Add(new FunctionCatalogIssue(source, i + 1, "A nested category is outside any category."));
						continue;
					}

					var category = new Builder(FunctionNodeKind.Category, line.Substring(SubCategoryMarker.Length), open.Count, i, line);
					open[open.Count - 1].Children.Add(category);
					open.Add(category);
				}
				else if (line.StartsWith(EndCategoryMarker, StringComparison.Ordinal))
				{
					if (open.Count > 1)
					{
						open[open.Count - 1].LastLine = i;
						open.RemoveAt(open.Count - 1);
					}
					else
					{
						issues.Add(new FunctionCatalogIssue(source, i + 1, "A closing marker has no matching nested category."));
					}
				}
				else if (line == SeparatorMarker)
				{
					roots.Add(new Builder(FunctionNodeKind.Separator, SeparatorName, 0, i, line));
					open.Clear();
				}
				else
				{
					Int32 d = line.IndexOf(DescriptionMarker, StringComparison.Ordinal);
					if (d <= 0)
					{
						issues.Add(new FunctionCatalogIssue(source, i + 1, "The line is not a category, a function or a separator."));
						continue;
					}

					if (open.Count == 0)
					{
						issues.Add(new FunctionCatalogIssue(source, i + 1, "A function is outside any category."));
						continue;
					}

					Int32 s = line.IndexOf(CodeMarker, d + DescriptionMarker.Length, StringComparison.Ordinal);
					String description;
					String code;
					if (s < 0)
					{
						issues.Add(new FunctionCatalogIssue(source, i + 1, "The function has no code marker; its code is empty."));
						description = line.Substring(d + DescriptionMarker.Length);
						code = String.Empty;
					}
					else
					{
						description = line.Substring(d + DescriptionMarker.Length, s - (d + DescriptionMarker.Length));
						code = line.Substring(s + CodeMarker.Length);
					}

					var function = new Builder(FunctionNodeKind.Function, line.Substring(0, d), open.Count, i, line)
					{
						Description = description,
						Code = code,
					};
					open[open.Count - 1].Children.Add(function);
				}
			}

			return new ParsedFunctionFile(roots.Select(r => r.Build(source)).ToList(), issues);
		}

		private sealed class Builder
		{
			internal Builder(FunctionNodeKind kind, String name, Int32 depth, Int32 line, String rawLine)
			{
				this.Kind = kind;
				this.Name = name;
				this.Depth = depth;
				this.Line = line;
				this.LastLine = line;
				this.RawLine = rawLine;
			}

			internal FunctionNodeKind Kind { get; }

			internal String Name { get; }

			internal Int32 Depth { get; }

			internal Int32 Line { get; }

			internal Int32 LastLine { get; set; }

			internal String RawLine { get; }

			internal String Description { get; init; } = String.Empty;

			internal String Code { get; init; } = String.Empty;

			internal List<Builder> Children { get; } = [];

			internal FunctionNode Build(FunctionSource source)
			{
				List<FunctionNode> children = this.Children.Select(c => c.Build(source)).ToList();
				Int32 last = this.LastLine;
				foreach (FunctionNode child in children)
				{
					last = Math.Max(last, child.LastLine);
				}

				return new FunctionNode(this.Kind, this.Name, source)
				{
					Description = this.Description,
					Code = this.Code,
					Depth = this.Depth,
					Children = children,
					SourceLine = this.Line,
					LastLine = last,
					RawLine = this.RawLine,
				};
			}
		}
	}
}