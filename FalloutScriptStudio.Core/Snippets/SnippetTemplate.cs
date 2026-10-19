namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// A parsed snippet body. The syntax is the one of the editors people know: <c>$1</c>, <c>${1}</c> and <c>${1:default}</c> are tab stops (a
	/// number used again is a mirror of the first one, and a default may hold other stops), <c>$0</c> is where the caret ends, <c>$NAME</c> and
	/// <c>${NAME}</c> are variables (see <see cref="SnippetVariables"/>; <c>${SELECTION:default}</c> gives the default when nothing is
	/// selected), and a backslash escapes <c>$</c>, <c>}</c> and <c>\</c>. A mistake never throws: it is listed in <see cref="Problems"/> and
	/// the mistaken part is plain text.
	/// </summary>
	public sealed class SnippetTemplate
	{
		private SnippetTemplate(String body, IReadOnlyList<SnippetPart> parts, IReadOnlyList<SnippetProblem> problems)
		{
			this.Body = body;
			this.Parts = parts;
			this.Problems = problems;
			this.UsesSelection = ContainsVariable(parts, SnippetVariables.Selection);
		}

		/// <summary>Gets the body that was parsed.</summary>
		public String Body { get; }

		/// <summary>Gets the mistakes of the body.</summary>
		public IReadOnlyList<SnippetProblem> Problems { get; }

		/// <summary>Gets a value indicating whether the body uses the selected text (it can surround code).</summary>
		public Boolean UsesSelection { get; }

		internal IReadOnlyList<SnippetPart> Parts { get; }

		/// <summary>Parses a body.</summary>
		/// <param name="body">The body.</param>
		/// <returns>The template.</returns>
		public static SnippetTemplate Parse(String body)
		{
			ArgumentNullException.ThrowIfNull(body);

			Parser parser = new Parser(body);
			IReadOnlyList<SnippetPart> parts = parser.ParseSequence(false);
			if (body.Trim().Length == 0)
			{
				parser.Problems.Add(new SnippetProblem(SnippetProblemKind.EmptyBody, 0, body.Length, String.Empty));
			}

			return new SnippetTemplate(body, parts, parser.Problems);
		}

		private static Boolean ContainsVariable(IReadOnlyList<SnippetPart> parts, String name)
		{
			foreach (SnippetPart part in parts)
			{
				if ((part.Kind == SnippetPartKind.Variable && String.Equals(part.Text, name, StringComparison.Ordinal)) || ContainsVariable(part.Children, name))
				{
					return true;
				}
			}

			return false;
		}

		private sealed class Parser
		{
			private readonly String _body;
			private Int32 _position;

			public Parser(String body)
			{
				this._body = body;
			}

			public List<SnippetProblem> Problems { get; } = [];

			/// <summary>Parses pieces up to the end of the body or, inside a placeholder, up to the unescaped <c>}</c> that closes it (which is not consumed).</summary>
			public List<SnippetPart> ParseSequence(Boolean insidePlaceholder)
			{
				List<SnippetPart> parts = [];
				StringBuilder text = new StringBuilder();
				Int32 plainBraces = 0;
				while (this._position < this._body.Length)
				{
					Char c = this._body[this._position];
					if (c == '\\' && this._position + 1 < this._body.Length && this._body[this._position + 1] is '$' or '}' or '\\')
					{
						text.Append(this._body[this._position + 1]);
						this._position += 2;
						continue;
					}

					if (c == '$')
					{
						Int32 start = this._position;
						Int32 problemCount = this.Problems.Count;
						SnippetPart? part = this.ParseDollar();
						if (part is null)
						{
							// Not a placeholder: the dollar sign is text.
							this._position = start + 1;
							text.Append('$');
							continue;
						}

						if (problemCount != this.Problems.Count && part.Kind == SnippetPartKind.Text)
						{
							text.Append(part.Text);
							continue;
						}

						Flush(parts, text);
						parts.Add(part);
						continue;
					}

					if (insidePlaceholder)
					{
						if (c == '{')
						{
							plainBraces++;
						}
						else if (c == '}')
						{
							if (plainBraces == 0)
							{
								break;
							}

							plainBraces--;
						}
					}

					text.Append(c);
					this._position++;
				}

				Flush(parts, text);
				return parts;
			}

			private static void Flush(List<SnippetPart> parts, StringBuilder text)
			{
				if (text.Length > 0)
				{
					parts.Add(SnippetPart.CreateText(text.ToString()));
					text.Clear();
				}
			}

			/// <summary>Parses the construct that starts at a dollar sign; <see langword="null"/> when it is not one (a problem is added when it looked like one).</summary>
			private SnippetPart? ParseDollar()
			{
				Int32 start = this._position;
				if (start + 1 >= this._body.Length)
				{
					return null;
				}

				Char next = this._body[start + 1];
				if (Char.IsAsciiDigit(next))
				{
					this._position = start + 1;
					return CreateStop(this.ReadNumber(), Array.Empty<SnippetPart>());
				}

				if (IsNameStart(next))
				{
					this._position = start + 1;
					String name = this.ReadName();
					return this.CreateVariable(name, Array.Empty<SnippetPart>(), start);
				}

				if (next != '{')
				{
					return null;
				}

				this._position = start + 2;
				if (this._position >= this._body.Length)
				{
					return this.Reject(SnippetProblemKind.UnterminatedPlaceholder, start);
				}

				Char first = this._body[this._position];
				Boolean isNumber = Char.IsAsciiDigit(first);
				Boolean isName = IsNameStart(first);
				if (!isNumber && !isName)
				{
					return this.Reject(SnippetProblemKind.InvalidPlaceholder, start);
				}

				Int32 index = 0;
				String variable = String.Empty;
				if (isNumber)
				{
					index = this.ReadNumber();
				}
				else
				{
					variable = this.ReadName();
				}

				IReadOnlyList<SnippetPart> children = Array.Empty<SnippetPart>();
				if (this._position < this._body.Length && this._body[this._position] == ':')
				{
					this._position++;
					children = this.ParseSequence(true);
				}

				if (this._position >= this._body.Length || this._body[this._position] != '}')
				{
					return this.Reject(this._position >= this._body.Length ? SnippetProblemKind.UnterminatedPlaceholder : SnippetProblemKind.InvalidPlaceholder, start);
				}

				this._position++;
				return isNumber ? CreateStop(index, children) : this.CreateVariable(variable, children, start);
			}

			private SnippetPart? Reject(SnippetProblemKind kind, Int32 start)
			{
				// What was parsed inside the mistaken construct is dropped with its problems: the whole construct is text.
				this.Problems.RemoveAll(problem => problem.Offset > start);
				Int32 end = Math.Min(start + 2, this._body.Length);
				this.Problems.Add(new SnippetProblem(kind, start, end - start, this._body.Substring(start, end - start)));
				this._position = start + 1;
				return SnippetPart.CreateText("$");
			}

			private SnippetPart CreateVariable(String name, IReadOnlyList<SnippetPart> children, Int32 start)
			{
				if (!SnippetVariables.IsKnown(name))
				{
					this.Problems.Add(new SnippetProblem(SnippetProblemKind.UnknownVariable, start, this._position - start, name));
					return SnippetPart.CreateText(this._body.Substring(start, this._position - start));
				}

				return new SnippetPart(SnippetPartKind.Variable, name, 0, children);
			}

			private static SnippetPart CreateStop(Int32 index, IReadOnlyList<SnippetPart> children)
			{
				return new SnippetPart(SnippetPartKind.Stop, String.Empty, index, children);
			}

			private static Boolean IsNameStart(Char c)
			{
				return c == '_' || Char.IsAsciiLetter(c);
			}

			private Int32 ReadNumber()
			{
				Int32 value = 0;
				while (this._position < this._body.Length && Char.IsAsciiDigit(this._body[this._position]))
				{
					value = Math.Min((value * 10) + (this._body[this._position] - '0'), 9999);
					this._position++;
				}

				return value;
			}

			private String ReadName()
			{
				Int32 start = this._position;
				while (this._position < this._body.Length && (this._body[this._position] == '_' || Char.IsAsciiLetterOrDigit(this._body[this._position])))
				{
					this._position++;
				}

				return this._body.Substring(start, this._position - start);
			}
		}
	}
}