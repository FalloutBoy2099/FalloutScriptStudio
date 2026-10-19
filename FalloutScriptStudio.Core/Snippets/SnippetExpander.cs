namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using TextTools;

	/// <summary>
	/// Turns the body of a snippet into the text to insert: the variables get their values, the lines are indented like the line of the
	/// insertion (a tab at the start of a line of the body is one indentation level of the document), the line breaks are the ones of the
	/// document, and the places of the tab stops are worked out. It is pure: the editor only has to insert the text and follow the stops.
	/// </summary>
	public static class SnippetExpander
	{
		private const Int32 MaxDepth = 8;

		/// <summary>Expands a body.</summary>
		/// <param name="body">The body.</param>
		/// <param name="context">The place of the insertion.</param>
		/// <returns>The text and its tab stops.</returns>
		public static SnippetExpansion Expand(String body, SnippetContext context)
		{
			ArgumentNullException.ThrowIfNull(body);
			return Expand(SnippetTemplate.Parse(body), context);
		}

		/// <summary>Expands a parsed body.</summary>
		/// <param name="template">The template.</param>
		/// <param name="context">The place of the insertion.</param>
		/// <returns>The text and its tab stops.</returns>
		public static SnippetExpansion Expand(SnippetTemplate template, SnippetContext context)
		{
			ArgumentNullException.ThrowIfNull(template);
			ArgumentNullException.ThrowIfNull(context);

			Dictionary<Int32, SnippetPart> definitions = [];
			FindDefinitions(template.Parts, definitions);
			Emitter emitter = new Emitter(context, definitions);
			emitter.EmitParts(template.Parts, 0);
			return emitter.Finish();
		}

		/// <summary>The place that defines a number is the first one that has a default text, otherwise the first one.</summary>
		private static void FindDefinitions(IReadOnlyList<SnippetPart> parts, Dictionary<Int32, SnippetPart> definitions)
		{
			foreach (SnippetPart part in parts)
			{
				if (part.Kind == SnippetPartKind.Stop && (!definitions.TryGetValue(part.Index, out SnippetPart? known) || (known.Children.Count == 0 && part.Children.Count > 0)))
				{
					definitions[part.Index] = part;
				}

				FindDefinitions(part.Children, definitions);
			}
		}

		private sealed class Emitter
		{
			private readonly SnippetContext _context;
			private readonly Dictionary<Int32, SnippetPart> _definitions;
			private readonly StringBuilder _text = new StringBuilder();
			private readonly Dictionary<Int32, List<TextSpan>> _occurrences = [];
			private readonly HashSet<Int32> _primaryDone = [];
			private readonly HashSet<Int32> _active = [];
			private String _lineIndent;
			private Boolean _atLineStart;
			private Int32 _finalOffset = -1;

			public Emitter(SnippetContext context, Dictionary<Int32, SnippetPart> definitions)
			{
				this._context = context;
				this._definitions = definitions;
				this._lineIndent = context.LineIndent;
			}

			public void EmitParts(IReadOnlyList<SnippetPart> parts, Int32 depth)
			{
				foreach (SnippetPart part in parts)
				{
					switch (part.Kind)
					{
						case SnippetPartKind.Text:
							this.AppendText(part.Text);
							break;
						case SnippetPartKind.Variable:
							this.EmitVariable(part, depth);
							break;
						default:
							this.EmitStop(part, depth);
							break;
					}
				}
			}

			public SnippetExpansion Finish()
			{
				List<SnippetStop> stops = [];
				String text = this._text.ToString();
				foreach (Int32 index in this._occurrences.Keys.Where(key => key > 0).OrderBy(key => key))
				{
					List<TextSpan> occurrences = this._occurrences[index];
					stops.Add(new SnippetStop(index, occurrences, text.Substring(occurrences[0].Offset, occurrences[0].Length)));
				}

				return new SnippetExpansion(text, stops, this._finalOffset >= 0 ? this._finalOffset : text.Length);
			}

			private void EmitStop(SnippetPart part, Int32 depth)
			{
				Int32 start = this._text.Length;
				if (part.Index == 0)
				{
					if (this._finalOffset < 0)
					{
						this._finalOffset = start;
					}

					return;
				}

				SnippetPart definition = this._definitions.TryGetValue(part.Index, out SnippetPart? known) ? known : part;
				Boolean primary = ReferenceEquals(definition, part) && !this._primaryDone.Contains(part.Index);
				if (primary)
				{
					_ = this._primaryDone.Add(part.Index);
				}

				if (depth < MaxDepth && this._active.Add(part.Index))
				{
					this.EmitParts(definition.Children, depth + 1);
					_ = this._active.Remove(part.Index);
				}

				if (!this._occurrences.TryGetValue(part.Index, out List<TextSpan>? list))
				{
					list = [];
					this._occurrences[part.Index] = list;
				}

				TextSpan span = new TextSpan(start, this._text.Length - start);
				if (primary)
				{
					list.Insert(0, span);
				}
				else
				{
					list.Add(span);
				}
			}

			private void EmitVariable(SnippetPart part, Int32 depth)
			{
				if (part.Text == SnippetVariables.Selection)
				{
					if (String.IsNullOrEmpty(this._context.Selection))
					{
						if (depth < MaxDepth)
						{
							this.EmitParts(part.Children, depth + 1);
						}
					}
					else
					{
						this.AppendSelection(this._context.Selection);
					}

					return;
				}

				String value = part.Text switch
				{
					SnippetVariables.CurrentProcedure => this._context.CurrentProcedure,
					SnippetVariables.FileName => this._context.FileName,
					SnippetVariables.ScriptName => this._context.ScriptName,
					SnippetVariables.Date => this._context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
					SnippetVariables.Time => this._context.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
					_ => String.Empty,
				};
				if (value.Length == 0 && depth < MaxDepth)
				{
					this.EmitParts(part.Children, depth + 1);
				}
				else
				{
					this.AppendText(value);
				}
			}

			/// <summary>Appends the text of the body: line breaks become the ones of the document and keep the indentation of the line, and the tabs at the start of a line become indentation levels.</summary>
			private void AppendText(String text)
			{
				for (Int32 i = 0; i < text.Length; i++)
				{
					Char c = text[i];
					if (c == '\r' || c == '\n')
					{
						if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
						{
							i++;
						}

						this._text.Append(this._context.NewLine).Append(this._context.LineIndent);
						this._lineIndent = this._context.LineIndent;
						this._atLineStart = true;
					}
					else if (c == '\t' && this._atLineStart)
					{
						this._text.Append(this._context.IndentUnit);
						this._lineIndent += this._context.IndentUnit;
					}
					else if (c == ' ' && this._atLineStart)
					{
						this._text.Append(' ');
						this._lineIndent += " ";
					}
					else
					{
						this._text.Append(c);
						this._atLineStart = false;
					}
				}
			}

			/// <summary>Appends the selected text: its lines keep their indentation relative to each other and follow the indentation of the line it is put on.</summary>
			private void AppendSelection(String selection)
			{
				String[] lines = selection.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
				String common = FindCommonIndent(lines);
				for (Int32 i = 0; i < lines.Length; i++)
				{
					String line = lines[i];
					if (i > 0)
					{
						this._text.Append(this._context.NewLine);
						if (line.Trim().Length > 0)
						{
							this._text.Append(this._lineIndent);
						}
					}

					if (line.StartsWith(common, StringComparison.Ordinal))
					{
						line = line.Substring(common.Length);
					}

					this._text.Append(line);
				}

				this._atLineStart = false;
			}

			private static String FindCommonIndent(String[] lines)
			{
				String? common = null;
				for (Int32 i = 0; i < lines.Length; i++)
				{
					String line = lines[i];
					if (line.Trim().Length == 0)
					{
						continue;
					}

					Int32 length = 0;
					while (length < line.Length && (line[length] == ' ' || line[length] == '\t'))
					{
						length++;
					}

					String indent = line.Substring(0, length);
					if (i == 0 && length == 0)
					{
						// The selection starts in the middle of a line: its first line does not tell the indentation of the others.
						continue;
					}

					if (common is null)
					{
						common = indent;
					}
					else
					{
						Int32 same = 0;
						while (same < common.Length && same < indent.Length && common[same] == indent[same])
						{
							same++;
						}

						common = common.Substring(0, same);
					}
				}

				return common ?? String.Empty;
			}
		}
	}
}