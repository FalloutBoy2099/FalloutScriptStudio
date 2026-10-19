namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;

	/// <summary>A text, its tokens and its lines: what every pass of the formatter works on.</summary>
	internal sealed class FormatModel
	{
		private FormatModel(String text, List<FormatToken> tokens, List<FormatLine> lines, String newLine)
		{
			this.Text = text;
			this.Tokens = tokens;
			this.Lines = lines;
			this.NewLine = newLine;
		}

		/// <summary>Gets the text.</summary>
		public String Text { get; }

		/// <summary>Gets the tokens (they cover the text).</summary>
		public List<FormatToken> Tokens { get; }

		/// <summary>Gets the lines (at least one).</summary>
		public List<FormatLine> Lines { get; }

		/// <summary>Gets the line break of the document: the first one found, <c>\r\n</c> when there is none.</summary>
		public String NewLine { get; }

		/// <summary>Builds the model of a text.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The model.</returns>
		public static FormatModel Build(String text)
		{
			List<FormatToken> tokens = FormatLexer.Lex(text);
			List<FormatLine> lines = [];
			String newLine = "\r\n";
			Boolean newLineKnown = false;
			Int32 start = 0;
			Int32 i = 0;
			while (i < text.Length)
			{
				Char c = text[i];
				if (c == '\r' || c == '\n')
				{
					Int32 eol = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
					if (!newLineKnown)
					{
						newLine = eol == 2 ? "\r\n" : c == '\n' ? "\n" : "\r";
						newLineKnown = true;
					}

					lines.Add(new FormatLine(lines.Count, start, i, eol));
					i += eol;
					start = i;
				}
				else
				{
					i++;
				}
			}

			lines.Add(new FormatLine(lines.Count, start, text.Length, 0));

			// Assign the tokens to the lines they start on, and find the lines that begin inside a multi-line token.
			Int32 lineIndex = 0;
			for (Int32 t = 0; t < tokens.Count; t++)
			{
				FormatToken token = tokens[t];
				while (lineIndex + 1 < lines.Count && lines[lineIndex].Next <= token.Start)
				{
					lineIndex++;
				}

				FormatLine line = lines[lineIndex];
				if (line.FirstToken < 0)
				{
					line.FirstToken = t;
				}

				if (token.Kind != FormatTokenKind.Newline)
				{
					line.EndToken = t + 1;
				}

				if (token.Kind is FormatTokenKind.BlockComment or FormatTokenKind.Directive && token.End > line.End + line.EolLength)
				{
					line.StartsMultiLineToken = true;
					Int32 inner = lineIndex + 1;
					while (inner < lines.Count && lines[inner].Start < token.End)
					{
						lines[inner].Verbatim = true;
						inner++;
					}
				}
			}

			// A line that no token starts on (inside a comment) gets an empty range at the place of the next token.
			Int32 nextFirst = tokens.Count;
			for (Int32 l = lines.Count - 1; l >= 0; l--)
			{
				FormatLine line = lines[l];
				if (line.FirstToken < 0)
				{
					line.FirstToken = nextFirst;
					line.EndToken = nextFirst;
				}
				else
				{
					nextFirst = line.FirstToken;
					if (line.EndToken < line.FirstToken)
					{
						line.EndToken = line.FirstToken;
					}
				}
			}

			return new FormatModel(text, tokens, lines, newLine);
		}

		/// <summary>Gets the text of a line without its line break.</summary>
		/// <param name="line">The line.</param>
		/// <returns>The text.</returns>
		public String GetLineText(FormatLine line)
		{
			return this.Text.Substring(line.Start, line.Length);
		}

		/// <summary>Gets the line break that ends a line, or the document's line break for the last line.</summary>
		/// <param name="line">The line.</param>
		/// <returns>The line break.</returns>
		public String GetEol(FormatLine line)
		{
			return line.EolLength == 0 ? this.NewLine : this.Text.Substring(line.End, line.EolLength);
		}

		/// <summary>Finds the first token of a line that is not white space or a comment.</summary>
		/// <param name="line">The line.</param>
		/// <returns>The index of the token, or -1 when the line holds no code.</returns>
		public Int32 FirstCodeToken(FormatLine line)
		{
			for (Int32 t = line.FirstToken; t < line.EndToken; t++)
			{
				if (!this.Tokens[t].IsTrivia)
				{
					return t;
				}
			}

			return -1;
		}

		/// <summary>Finds the last token of a line that is not white space or a comment.</summary>
		/// <param name="line">The line.</param>
		/// <returns>The index of the token, or -1 when the line holds no code.</returns>
		public Int32 LastCodeToken(FormatLine line)
		{
			for (Int32 t = line.EndToken - 1; t >= line.FirstToken; t--)
			{
				if (!this.Tokens[t].IsTrivia)
				{
					return t;
				}
			}

			return -1;
		}

		/// <summary>Determines whether a line is empty or holds only white space.</summary>
		/// <param name="line">The line.</param>
		/// <returns><see langword="true"/> when it is blank.</returns>
		public Boolean IsBlank(FormatLine line)
		{
			for (Int32 i = line.Start; i < line.End; i++)
			{
				if (!Char.IsWhiteSpace(this.Text[i]))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Determines whether the first token of a line is a comment and no code follows it on the line.</summary>
		/// <param name="line">The line.</param>
		/// <returns><see langword="true"/> for a line that only holds a comment.</returns>
		public Boolean IsCommentOnly(FormatLine line)
		{
			Boolean comment = false;
			for (Int32 t = line.FirstToken; t < line.EndToken; t++)
			{
				FormatToken token = this.Tokens[t];
				if (token.IsComment)
				{
					comment = true;
				}
				else if (token.Kind != FormatTokenKind.Space)
				{
					return false;
				}
			}

			return comment;
		}

		/// <summary>Gets the characters of the leading white space of a line.</summary>
		/// <param name="line">The line.</param>
		/// <returns>The spaces and tabs the line starts with.</returns>
		public String GetIndent(FormatLine line)
		{
			Int32 i = line.Start;
			while (i < line.End && (this.Text[i] == ' ' || this.Text[i] == '\t'))
			{
				i++;
			}

			return this.Text.Substring(line.Start, i - line.Start);
		}
	}
}