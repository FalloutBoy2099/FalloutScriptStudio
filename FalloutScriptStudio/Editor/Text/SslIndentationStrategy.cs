namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Indentation;

	/// <summary>
	/// AvalonEdit indentation strategy for SSL: a new line keeps the indentation of the previous line and goes one level
	/// deeper after <c>begin</c> / <c>then</c> / <c>else</c> / <c>do</c>; a closing <c>end</c> typed on its own line is
	/// aligned with its <c>begin</c>; Enter after a <c>begin</c> line closes the block with an <c>end</c> line when there is
	/// room for it (legacy <c>DefaultFormattingStrategy.GetIndentation</c>).
	/// </summary>
	public sealed class SslIndentationStrategy : IIndentationStrategy
	{
		private readonly Func<String> _unitProvider;

		/// <summary>Initializes a new instance of the <see cref="SslIndentationStrategy"/> class.</summary>
		/// <param name="unitProvider">Returns one indentation level (the editor's current tab or spaces string).</param>
		public SslIndentationStrategy(Func<String> unitProvider)
		{
			this._unitProvider = unitProvider;
		}

		/// <summary>
		/// Gets or sets the test that tells whether a line (1-based) starts a folding region. A <c>procedure ... begin</c> line that
		/// starts a fold already has its <c>end</c>, so no <c>end</c> is added after it.
		/// </summary>
		public Func<Int32, Boolean>? IsFoldStart { get; set; }

		/// <summary>
		/// Indents the line that Enter just created. When the previous line is a <c>begin</c> line and the line below the new line
		/// is empty (or does not exist yet), an <c>end</c> line is added there.
		/// </summary>
		/// <param name="document">The document.</param>
		/// <param name="line">The new line (its previous line is the one Enter was pressed on).</param>
		public void IndentLine(TextDocument document, DocumentLine line)
		{
			DocumentLine? previous = line.PreviousLine;
			if (previous is null)
			{
				return;
			}

			this.CloseBlock(document, line, previous);
			this.IndentCore(document, line, previous);
		}

		/// <inheritdoc />
		public void IndentLines(TextDocument document, Int32 beginLine, Int32 endLine)
		{
			using (document.RunUpdate())
			{
				for (Int32 i = beginLine; i <= endLine; i++)
				{
					DocumentLine line = document.GetLineByNumber(i);
					if (line.PreviousLine is DocumentLine previous)
					{
						this.IndentCore(document, line, previous);
					}
				}
			}
		}

		/// <summary>
		/// Re-aligns the line that was just completed with <c>end</c> with the matching <c>begin</c>.
		/// </summary>
		/// <param name="document">The document.</param>
		/// <param name="line">The line that contains the typed <c>end</c>.</param>
		/// <returns><see langword="true"/> when the indentation was changed.</returns>
		public Boolean AlignClosingEnd(TextDocument document, DocumentLine line)
		{
			String text = document.GetText(line);
			if (SslIndentationRules.GetCode(text) != "end")
			{
				return false;
			}

			String? target = SslIndentationRules.FindBlockOpenerIndentation(
				index => document.GetText(document.GetLineByNumber(index + 1)),
				line.LineNumber - 1);
			if (target is null || target == SslIndentationRules.GetLeadingWhitespace(text))
			{
				return false;
			}

			SetIndentation(document, line, target);
			return true;
		}

		private void IndentCore(TextDocument document, DocumentLine line, DocumentLine previous)
		{
			String previousText = document.GetText(previous);
			String indentation = SslIndentationRules.GetNextLineIndentation(previousText, this._unitProvider());

			String lineCode = SslIndentationRules.GetCode(document.GetText(line));
			if (SslIndentationRules.StartsWithWord(lineCode, "end") && SslIndentationRules.OpensBlock(previousText))
			{
				indentation = SslIndentationRules.GetLeadingWhitespace(previousText);
			}

			SetIndentation(document, line, indentation);
		}

		/// <summary>
		/// Adds the <c>end</c> of a block that was just opened, unless the block is already closed further down (begin/end depth
		/// scan of the whole text). The text below the new line decides: the line right under it must
		/// be empty (or missing, at the end of the document). After an <c>if ... begin</c> one more line is needed, and an
		/// <c>else begin</c> is added too when that following line ends with <c>end</c>.
		/// </summary>
		private void CloseBlock(TextDocument document, DocumentLine line, DocumentLine begin)
		{
			String beginText = document.GetText(begin);
			String code = SslIndentationRules.GetCode(beginText);
			if (!SslIndentationRules.EndsWithWord(code, "begin"))
			{
				return;
			}

			if (SslIndentationRules.StartsWithWord(code, "procedure") && this.IsFoldStart?.Invoke(begin.LineNumber) == true)
			{
				return;
			}

			if (SslIndentationRules.IsBlockClosed(document.Text, begin.EndOffset))
			{
				return;
			}

			String newLine = TextUtilities.GetNewLineFromDocument(document, line.LineNumber);
			String indentation = SslIndentationRules.GetLeadingWhitespace(beginText);
			Int32 below = line.LineNumber + 1;
			Boolean added = false;
			if (below > document.LineCount)
			{
				// The new line is the last one: it needs a successor to hold the "end".
				if (!String.IsNullOrWhiteSpace(document.GetText(line)))
				{
					return;
				}

				AppendLine(document, newLine);
				added = true;
			}
			else if (!String.IsNullOrWhiteSpace(document.GetText(document.GetLineByNumber(below))))
			{
				return;
			}

			if (!SslIndentationRules.StartsWithWord(code, "if"))
			{
				document.Insert(document.GetLineByNumber(below).Offset, indentation + "end" + (added ? String.Empty : newLine));
				return;
			}

			Int32 after = below + 1;
			Boolean extended = false;
			if (after > document.LineCount)
			{
				AppendLine(document, newLine);
				extended = true;
			}

			String afterText = document.GetText(document.GetLineByNumber(after));
			String terminator = extended ? String.Empty : newLine;
			document.Insert(document.GetLineByNumber(below).Offset, indentation + "end" + terminator);
			if (afterText.TrimEnd(' ', '\t').EndsWith(" end", StringComparison.OrdinalIgnoreCase))
			{
				document.Insert(document.GetLineByNumber(after).Offset, indentation + "else begin" + terminator);
			}
		}

		/// <summary>Adds a line break at the end of the document without pulling the caret (which sits at the end) along.</summary>
		private static void AppendLine(TextDocument document, String newLine)
		{
			document.Insert(document.TextLength, newLine, AnchorMovementType.BeforeInsertion);
		}

		private static void SetIndentation(TextDocument document, DocumentLine line, String indentation)
		{
			ISegment whitespace = TextUtilities.GetWhitespaceAfter(document, line.Offset);
			if (document.GetText(whitespace) != indentation)
			{
				document.Replace(whitespace.Offset, whitespace.Length, indentation);
			}
		}
	}
}
