namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using Formatting;

	/// <summary>
	/// Default <see cref="ICodeFormatter"/>. With the default <see cref="FormatOptions"/>, operators (<c>:= != == &gt;= &lt;= += -= *= /= %= , &gt; &lt; + - * / %</c>)
	/// get a space on each side (commas only on the right); unary minus, <c>++</c>/<c>--</c>, strings and comments are left alone. The
	/// other options (indentation, <c>begin</c> / <c>else</c> placement, blank lines, wrapping...) are in <see cref="FormatOptions"/>.
	/// </summary>
	/// <remarks>
	/// Differences from the legacy formatter: comments (<c>//</c> tails and <c>/* */</c> blocks, also across lines) are not
	/// touched, the string positions are recomputed after every inserted space, and no exception is thrown for an operator at the
	/// very start or end of a line. The text of an <c>#include</c> line is never touched.
	/// </remarks>
	public sealed class CodeFormatter : ICodeFormatter
	{
		/// <inheritdoc />
		public String Format(String text)
		{
			return this.Format(text, FormatOptions.Default);
		}

		/// <inheritdoc />
		public String Format(String text, FormatOptions options)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(options);

			return FormatPipeline.Format(text, options);
		}

		/// <inheritdoc />
		public TextEdit? FormatSelectionOrLine(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			return this.FormatPiece(text, selection, FormatOptions.Default);
		}

		/// <inheritdoc />
		public IReadOnlyList<TextEdit> GetSelectionEdits(String text, TextSpan selection, FormatOptions options)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(options);

			if (!options.IsStructural)
			{
				TextEdit? edit = this.FormatPiece(text, selection, options);
				return edit is null ? Array.Empty<TextEdit>() : new[] { edit };
			}

			return LineDiff.Restrict(text, this.GetDocumentEdits(text, options), selection);
		}

		/// <inheritdoc />
		public IReadOnlyList<TextEdit> GetDocumentEdits(String text, FormatOptions options)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(options);

			return LineDiff.Compute(text, this.Format(text, options));
		}

		/// <inheritdoc />
		public SmartFormatResult? FormatSmart(String text, Int32 caretOffset)
		{
			return this.FormatSmart(text, caretOffset, FormatOptions.Default);
		}

		/// <inheritdoc />
		public SmartFormatResult? FormatSmart(String text, Int32 caretOffset, FormatOptions options)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(options);

			var map = new TextLineMap(text);
			Int32 line = map.GetLineFromOffset(caretOffset);
			Int32 lineStart = map.GetLineStart(line);
			Int32 column = Math.Min(Math.Max(caretOffset - lineStart, 0), map.GetLineLength(line));

			String before = text.Substring(lineStart, column);
			String formatted = SpacingPass.Apply(before, options);
			Int32 growth = formatted.Length - before.Length;
			if (growth <= 0)
			{
				return null;
			}

			return new SmartFormatResult(new TextEdit(lineStart, before.Length, formatted), caretOffset + growth);
		}

		private TextEdit? FormatPiece(String text, TextSpan selection, FormatOptions options)
		{
			Int32 offset;
			Int32 length;
			if (selection.IsEmpty)
			{
				var map = new TextLineMap(text);
				Int32 line = map.GetLineFromOffset(selection.Offset);
				offset = map.GetLineStart(line);
				length = map.GetLineLength(line);
			}
			else
			{
				offset = selection.Offset;
				length = selection.Length;
			}

			String original = text.Substring(offset, length);
			String formatted = this.Format(original, options);
			return String.Equals(original, formatted, StringComparison.Ordinal) ? null : new TextEdit(offset, length, formatted);
		}
	}
}