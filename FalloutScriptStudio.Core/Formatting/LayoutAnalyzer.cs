namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Follows the structure of a script (blocks opened by <c>begin</c> and closed by <c>end</c>, the bodies of <c>then</c> / <c>else</c> /
	/// <c>do</c> and of <c>case</c> labels, open brackets, statements that go on in the next line) to tell where every line should start.
	/// It is tolerant: unbalanced text gives a result, not an exception.
	/// </summary>
	internal static class LayoutAnalyzer
	{
		private static readonly HashSet<String> _infixWords = new(StringComparer.OrdinalIgnoreCase)
		{
			"and", "or", "bwand", "bwor", "bwxor", "in", "then", "do", "else", "begin", "end",
		};

		private static readonly HashSet<String> _expectsOperand = new(StringComparer.OrdinalIgnoreCase)
		{
			"return", "if", "while", "then", "else", "do", "not", "and", "or", "bwand", "bwor", "bwxor", "bwnot", "in", "call", "case", "begin", "end", "for", "foreach", "switch", "default", "variable", "import", "export", "procedure",
		};

		/// <summary>Analyzes a text.</summary>
		/// <param name="model">The model of the text.</param>
		/// <param name="options">The options (the indentation unit and the continuation style).</param>
		/// <returns>The result.</returns>
		public static LayoutInfo Analyze(FormatModel model, FormatOptions options)
		{
			String text = model.Text;
			List<FormatToken> tokens = model.Tokens;
			List<FormatLine> lines = model.Lines;
			LayoutInfo info = new LayoutInfo(lines.Count, tokens.Count);
			Analysis analysis = new Analysis(model, options, info);

			for (Int32 li = 0; li < lines.Count; li++)
			{
				FormatLine line = lines[li];
				Int32 firstCode = model.FirstCodeToken(line);
				if (firstCode < 0)
				{
					if (!line.Verbatim && !line.StartsMultiLineToken && model.IsCommentOnly(line))
					{
						analysis.Pending.Add(li);
					}

					continue;
				}

				if (tokens[firstCode].Kind == FormatTokenKind.Directive)
				{
					analysis.FlushPending(analysis.State.CountingFrames, false);
					analysis.HandleDirective(tokens[firstCode].GetText(text));
					continue;
				}

				analysis.ProcessLine(li, firstCode);
			}

			analysis.FlushPending(analysis.State.CountingFrames, false);
			return info;
		}

		private enum FrameOwner
		{
			Procedure,
			Then,
			Else,
			Do,
			Case,
			Other,
		}

		private sealed record Frame(Boolean IsBlock, FrameOwner Owner, Boolean Counts);

		private sealed record Bracket(Int32 Column, Boolean HasContent);

		private sealed class State
		{
			public List<Frame> Frames { get; private set; } = [];

			public List<Bracket> Brackets { get; private set; } = [];

			public Boolean StatementOpen { get; set; }

			public Int32 StatementLevel { get; set; }

			public Int32 StatementLine { get; set; }

			public String StatementFirstWord { get; set; } = String.Empty;

			public Boolean PendingElse { get; set; }

			public Boolean LastWasOperandEnd { get; set; }

			public Int32 CountingFrames
			{
				get
				{
					Int32 count = 0;
					foreach (Frame frame in this.Frames)
					{
						if (frame.Counts)
						{
							count++;
						}
					}

					return count;
				}
			}

			public Frame? Top => this.Frames.Count == 0 ? null : this.Frames[this.Frames.Count - 1];

			public State Clone()
			{
				return new State
				{
					Frames = new(this.Frames),
					Brackets = new(this.Brackets),
					StatementOpen = this.StatementOpen,
					StatementLevel = this.StatementLevel,
					StatementLine = this.StatementLine,
					StatementFirstWord = this.StatementFirstWord,
					PendingElse = this.PendingElse,
					LastWasOperandEnd = this.LastWasOperandEnd,
				};
			}

			public void Pop()
			{
				this.Frames.RemoveAt(this.Frames.Count - 1);
			}

			/// <summary>A construct (a statement, or a block that ended) is complete: the bodies that waited for it are complete too, except a body of <c>then</c>, which an <c>else</c> may still follow.</summary>
			public void CloseConstruct(FrameOwner owner)
			{
				if (owner == FrameOwner.Then)
				{
					this.PendingElse = true;
					return;
				}

				while (this.Top is { IsBlock: false } body && body.Owner != FrameOwner.Case)
				{
					this.Pop();
					if (body.Owner == FrameOwner.Then)
					{
						this.PendingElse = true;
						return;
					}
				}
			}

			public void ResolvePendingElse()
			{
				while (this.PendingElse)
				{
					this.PendingElse = false;
					this.CloseConstruct(FrameOwner.Other);
				}
			}

			public void DiscardBodiesToBlock()
			{
				while (this.Top is { IsBlock: false })
				{
					this.Pop();
				}
			}
		}

		private sealed class Conditional
		{
			public Conditional(State snapshot)
			{
				this.Snapshot = snapshot;
			}

			public State Snapshot { get; }

			public State? FirstBranchEnd { get; set; }
		}

		private sealed class Analysis
		{
			private readonly FormatModel _model;
			private readonly FormatOptions _options;
			private readonly LayoutInfo _info;
			private readonly Stack<Conditional> _conditionals = new Stack<Conditional>();
			private Int32 _line;
			private Int32 _lineLevel;
			private Int32 _lastCodeOnLine;
			private Int32 _indentWidth;
			private Int32 _contentStart;
			private Int32 _columnPosition;
			private Int32 _columnValue;

			public Analysis(FormatModel model, FormatOptions options, LayoutInfo info)
			{
				this._model = model;
				this._options = options;
				this._info = info;
			}

			public State State { get; private set; } = new State();

			public List<Int32> Pending { get; } = [];

			public void FlushPending(Int32 level, Boolean continuation)
			{
				if (this.Pending.Count == 0)
				{
					return;
				}

				String indent = this.IndentFor(level);
				foreach (Int32 line in this.Pending)
				{
					this._info.Indents[line] = indent;
					this._info.IsContinuation[line] = continuation;
					this._info.StatementLine[line] = line;
				}

				this.Pending.Clear();
			}

			public void HandleDirective(String directive)
			{
				Int32 i = 1;
				while (i < directive.Length && (directive[i] == ' ' || directive[i] == '\t'))
				{
					i++;
				}

				Int32 start = i;
				while (i < directive.Length && Char.IsLetter(directive[i]))
				{
					i++;
				}

				String name = directive.Substring(start, i - start).ToLowerInvariant();
				switch (name)
				{
					case "if":
					case "ifdef":
					case "ifndef":
						this._conditionals.Push(new Conditional(this.State.Clone()));
						break;
					case "else":
					case "elif":
						if (this._conditionals.Count > 0)
						{
							Conditional conditional = this._conditionals.Peek();
							conditional.FirstBranchEnd ??= this.State;
							this.State = conditional.Snapshot.Clone();
						}

						break;
					case "endif":
						if (this._conditionals.Count > 0)
						{
							Conditional conditional = this._conditionals.Pop();
							if (conditional.FirstBranchEnd is not null)
							{
								this.State = conditional.FirstBranchEnd;
							}
						}

						break;
					default:
						break;
				}
			}

			public void ProcessLine(Int32 lineIndex, Int32 firstCode)
			{
				FormatLine line = this._model.Lines[lineIndex];
				this._line = lineIndex;
				this._lastCodeOnLine = this._model.LastCodeToken(line);
				String existingIndent = this._model.GetIndent(line);
				this._contentStart = line.Start + existingIndent.Length;
				this._indentWidth = this.Width(existingIndent, 0);
				this._columnPosition = this._contentStart;
				this._columnValue = this._indentWidth;
				this._lineLevel = this.State.CountingFrames;
				this._info.StatementLine[lineIndex] = lineIndex;

				for (Int32 t = firstCode; t < line.EndToken; t++)
				{
					FormatToken token = this._model.Tokens[t];
					if (token.IsTrivia || token.Kind == FormatTokenKind.Directive)
					{
						continue;
					}

					this.ProcessToken(t, token, t == firstCode && !line.Verbatim);
				}
			}

			private void ProcessToken(Int32 t, FormatToken token, Boolean firstOnLine)
			{
				String text = this._model.Text;
				String word = token.Kind == FormatTokenKind.Word ? token.GetText(text).ToLowerInvariant() : String.Empty;
				State state = this.State;
				this._info.DepthBefore[t] = state.Brackets.Count;

				if (state.PendingElse && word != "else")
				{
					state.ResolvePendingElse();
				}

				if (firstOnLine)
				{
					this.StartLine(token, word);
				}

				switch (token.Kind)
				{
					case FormatTokenKind.Word:
						this.ProcessWord(t, word);
						break;
					case FormatTokenKind.Symbol:
						this.ProcessSymbol(t, token);
						break;
					default:
						this.StartStatement(String.Empty);
						state.LastWasOperandEnd = true;
						break;
				}
			}

			private void StartLine(FormatToken token, String word)
			{
				State state = this.State;
				Boolean startsOperand = token.Kind is FormatTokenKind.Number or FormatTokenKind.String
					|| (token.Kind == FormatTokenKind.Word && !_infixWords.Contains(word));
				if (state.StatementOpen && state.Brackets.Count == 0 && state.LastWasOperandEnd && startsOperand && word != "case" && word != "default")
				{
					// Two operands in a row cannot belong to one statement: the previous one lacks its semicolon.
					state.StatementOpen = false;
					state.CloseConstruct(FrameOwner.Other);
					if (state.PendingElse && word != "else")
					{
						state.ResolvePendingElse();
					}
				}

				Int32 level;
				Boolean continuation = false;
				String indent;
				switch (word)
				{
					case "end":
						state.DiscardBodiesToBlock();
						level = Math.Max(state.CountingFrames - 1, 0);
						state.StatementOpen = false;
						indent = this.IndentFor(level);
						this.FlushPending(state.CountingFrames, false);
						break;
					case "begin":
						if (state.StatementOpen)
						{
							level = state.StatementLevel;
						}
						else if (state.Top is { IsBlock: false, Owner: not FrameOwner.Case, Counts: true })
						{
							level = Math.Max(state.CountingFrames - 1, 0);
						}
						else
						{
							level = state.CountingFrames;
						}

						indent = this.IndentFor(level);
						this.FlushPending(level, false);
						break;
					case "case":
					case "default":
						if (state.Top is { IsBlock: false, Owner: FrameOwner.Case })
						{
							state.Pop();
						}

						state.StatementOpen = false;
						level = state.CountingFrames;
						indent = this.IndentFor(level);
						this.FlushPending(level, false);
						break;
					default:
						if (state.StatementOpen && word != "else")
						{
							continuation = true;
							level = state.StatementLevel;
							indent = this.ContinuationIndent(level);
						}
						else
						{
							level = state.CountingFrames;
							indent = this.IndentFor(level);
						}

						this.FlushPending(level, continuation);
						break;
				}

				this._lineLevel = level;
				this._info.Indents[this._line] = indent;
				this._info.IsContinuation[this._line] = continuation;
				this._info.StatementLine[this._line] = continuation ? state.StatementLine : this._line;
				this._indentWidth = this.Width(indent, 0);
				this._columnPosition = this._contentStart;
				this._columnValue = this._indentWidth;
			}

			private void ProcessWord(Int32 t, String word)
			{
				State state = this.State;
				switch (word)
				{
					case "begin":
						this.OpenBlock();
						state.StatementOpen = false;
						state.LastWasOperandEnd = false;
						break;
					case "end":
						this.CloseBlock();
						state.StatementOpen = false;
						state.LastWasOperandEnd = false;
						break;
					case "then":
						state.Frames.Add(new Frame(false, FrameOwner.Then, !this.NextIsOnSameLine(t)));
						state.StatementOpen = false;
						state.LastWasOperandEnd = false;
						break;
					case "do":
						state.Frames.Add(new Frame(false, FrameOwner.Do, !this.NextIsOnSameLine(t)));
						state.StatementOpen = false;
						state.LastWasOperandEnd = false;
						break;
					case "else":
						state.PendingElse = false;
						state.Frames.Add(new Frame(false, FrameOwner.Else, !this.NextIsOnSameLine(t)));
						state.StatementOpen = false;
						state.LastWasOperandEnd = false;
						break;
					case "case":
					case "default":
						if (state.Top is { IsBlock: false, Owner: FrameOwner.Case })
						{
							state.Pop();
						}

						state.StatementOpen = false;
						this.StartStatement(word);
						state.LastWasOperandEnd = false;
						break;
					default:
						this.StartStatement(word);
						state.LastWasOperandEnd = !_expectsOperand.Contains(word);
						break;
				}
			}

			private void ProcessSymbol(Int32 t, FormatToken token)
			{
				State state = this.State;
				String text = this._model.Text;
				Char c = token.Length == 1 ? text[token.Start] : '\0';
				switch (c)
				{
					case '(':
					case '[':
					case '{':
						this.StartStatement(String.Empty);
						Int32 afterBracket = t + 1 < this._model.Tokens.Count && this._model.Tokens[t + 1].Kind == FormatTokenKind.Space ? this._model.Tokens[t + 1].End : token.End;
						state.Brackets.Add(new Bracket(this.ColumnAt(afterBracket), this._lastCodeOnLine > t));
						state.LastWasOperandEnd = false;
						break;
					case ')':
					case ']':
					case '}':
						if (state.Brackets.Count > 0)
						{
							state.Brackets.RemoveAt(state.Brackets.Count - 1);
						}

						state.LastWasOperandEnd = true;
						break;
					case ';':
						if (state.Brackets.Count == 0)
						{
							state.StatementOpen = false;
							state.CloseConstruct(FrameOwner.Other);
						}

						state.LastWasOperandEnd = false;
						break;
					case ':':
						if (state.Brackets.Count == 0 && state.StatementOpen && (state.StatementFirstWord == "case" || state.StatementFirstWord == "default"))
						{
							state.Frames.Add(new Frame(false, FrameOwner.Case, !this.BeginFollows(t)));
							state.StatementOpen = false;
						}
						else
						{
							this.StartStatement(String.Empty);
						}

						state.LastWasOperandEnd = false;
						break;
					default:
						this.StartStatement(String.Empty);
						state.LastWasOperandEnd = false;
						break;
				}
			}

			private void OpenBlock()
			{
				State state = this.State;
				if (state.Top is { IsBlock: false } body && (body.Owner != FrameOwner.Case || !body.Counts))
				{
					// The body of a `case` that starts with the `begin` on the line of its label is the block itself.
					state.Pop();
					state.Frames.Add(new Frame(true, body.Owner == FrameOwner.Case ? FrameOwner.Other : body.Owner, true));
					return;
				}

				Boolean procedure = state.StatementOpen && state.StatementFirstWord == "procedure" && state.Frames.Count == 0;
				if (procedure)
				{
					this._info.ProcedureStartLines.Add(state.StatementLine);
				}

				state.Frames.Add(new Frame(true, procedure ? FrameOwner.Procedure : FrameOwner.Other, true));
			}

			private void CloseBlock()
			{
				State state = this.State;
				state.DiscardBodiesToBlock();
				if (state.Top is not { IsBlock: true } block)
				{
					return;
				}

				state.Pop();
				if (block.Owner == FrameOwner.Procedure)
				{
					this._info.ProcedureEndLines.Add(this._line);
				}

				state.CloseConstruct(block.Owner);
			}

			private void StartStatement(String word)
			{
				State state = this.State;
				if (!state.StatementOpen)
				{
					state.StatementOpen = true;
					state.StatementLevel = this._lineLevel;
					state.StatementLine = this._line;
					state.StatementFirstWord = word;
				}
			}

			/// <summary>Tells whether the next code token on the line of a token is <c>begin</c>.</summary>
			private Boolean BeginFollows(Int32 t)
			{
				List<FormatToken> tokens = this._model.Tokens;
				for (Int32 i = t + 1; i < tokens.Count; i++)
				{
					if (tokens[i].Kind is FormatTokenKind.Space or FormatTokenKind.BlockComment)
					{
						continue;
					}

					return tokens[i].IsWord(this._model.Text, "begin");
				}

				return false;
			}

			/// <summary>Tells whether code follows a token on its line (so that a body starts there and does not add a level).</summary>
			private Boolean NextIsOnSameLine(Int32 t)
			{
				List<FormatToken> tokens = this._model.Tokens;
				for (Int32 i = t + 1; i < tokens.Count; i++)
				{
					switch (tokens[i].Kind)
					{
						case FormatTokenKind.Space:
						case FormatTokenKind.BlockComment:
							continue;
						case FormatTokenKind.Newline:
						case FormatTokenKind.LineComment:
						case FormatTokenKind.Directive:
							return false;
						default:
							return true;
					}
				}

				return false;
			}

			private String ContinuationIndent(Int32 statementLevel)
			{
				State state = this.State;
				if (this._options.AlignContinuationToBracket && state.Brackets.Count > 0 && state.Brackets[state.Brackets.Count - 1] is { HasContent: true } bracket)
				{
					return this.IndentForColumn(bracket.Column);
				}

				return this.IndentFor(statementLevel + this._options.ContinuationIndent);
			}

			private String IndentFor(Int32 level)
			{
				return this._options.UseTabs ? new String('\t', level) : new String(' ', level * this._options.IndentSize);
			}

			private String IndentForColumn(Int32 column)
			{
				if (this._options.UseTabs)
				{
					return new String('\t', column / this._options.IndentSize) + new String(' ', column % this._options.IndentSize);
				}

				return new String(' ', column);
			}

			/// <summary>Gets the column just after a position of the current line, with the indentation the line will have.</summary>
			private Int32 ColumnAt(Int32 position)
			{
				String text = this._model.Text;
				Int32 size = this._options.IndentSize;
				for (Int32 i = this._columnPosition; i < position; i++)
				{
					this._columnValue += text[i] == '\t' ? size - (this._columnValue % size) : 1;
				}

				this._columnPosition = Math.Max(position, this._columnPosition);
				return this._columnValue;
			}

			private Int32 Width(String indent, Int32 start)
			{
				Int32 column = start;
				Int32 size = this._options.IndentSize;
				foreach (Char c in indent)
				{
					column += c == '\t' ? size - (column % size) : 1;
				}

				return column;
			}
		}
	}
}