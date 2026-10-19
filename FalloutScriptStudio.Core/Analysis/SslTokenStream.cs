namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>The tokens of one script text together with the text, and a few helpers to compare them.</summary>
	public sealed class SslTokenStream
	{
		private readonly SslToken[] _tokens;

		/// <summary>Initializes a new instance of the <see cref="SslTokenStream"/> class.</summary>
		/// <param name="text">The text that was tokenized.</param>
		/// <param name="tokens">The tokens, in order of their offset.</param>
		public SslTokenStream(String text, SslToken[] tokens)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(tokens);
			this.Text = text;
			this._tokens = tokens;
		}

		/// <summary>Gets the tokenized text.</summary>
		public String Text { get; }

		/// <summary>Gets the number of tokens.</summary>
		public Int32 Count => this._tokens.Length;

		/// <summary>Gets a token.</summary>
		/// <param name="index">The index of the token.</param>
		public SslToken this[Int32 index] => this._tokens[index];

		/// <summary>Gets the characters of a token.</summary>
		/// <param name="index">The index of the token.</param>
		/// <returns>The characters.</returns>
		public ReadOnlySpan<Char> Span(Int32 index)
		{
			SslToken token = this._tokens[index];
			return this.Text.AsSpan(token.Offset, token.Length);
		}

		/// <summary>Gets the text of a token.</summary>
		/// <param name="index">The index of the token.</param>
		/// <returns>The text.</returns>
		public String GetText(Int32 index)
		{
			SslToken token = this._tokens[index];
			return this.Text.Substring(token.Offset, token.Length);
		}

		/// <summary>Determines whether a token is the given word (the comparison ignores the case).</summary>
		/// <param name="index">The index of the token; an index outside the stream gives <see langword="false"/>.</param>
		/// <param name="keyword">The word.</param>
		/// <returns><see langword="true"/> for an identifier equal to the word.</returns>
		public Boolean IsKeyword(Int32 index, String keyword)
		{
			return index >= 0 && index < this._tokens.Length
				&& this._tokens[index].Kind == SslTokenKind.Identifier
				&& this.Span(index).Equals(keyword, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Determines whether a token is the given operator or separator.</summary>
		/// <param name="index">The index of the token; an index outside the stream gives <see langword="false"/>.</param>
		/// <param name="punctuation">The operator, for example <c>;</c> or <c>:=</c>.</param>
		/// <returns><see langword="true"/> for a punctuation token with exactly that text.</returns>
		public Boolean IsPunctuation(Int32 index, String punctuation)
		{
			return index >= 0 && index < this._tokens.Length
				&& this._tokens[index].Kind == SslTokenKind.Punctuation
				&& this.Span(index).SequenceEqual(punctuation);
		}

		/// <summary>Determines whether a token is the given single character operator or separator.</summary>
		/// <param name="index">The index of the token; an index outside the stream gives <see langword="false"/>.</param>
		/// <param name="punctuation">The character, for example <c>;</c>.</param>
		/// <returns><see langword="true"/> for a punctuation token that is just that character.</returns>
		public Boolean IsPunctuation(Int32 index, Char punctuation)
		{
			return index >= 0 && index < this._tokens.Length
				&& this._tokens[index].Kind == SslTokenKind.Punctuation
				&& this._tokens[index].Length == 1
				&& this.Text[this._tokens[index].Offset] == punctuation;
		}

		/// <summary>Gets the name of a preprocessor directive token.</summary>
		/// <param name="index">The index of a <see cref="SslTokenKind.Directive"/> token.</param>
		/// <returns>The lower-case name without the <c>#</c> (for example <c>define</c>), or an empty text for another token.</returns>
		public String GetDirectiveName(Int32 index)
		{
			if (index < 0 || index >= this._tokens.Length || this._tokens[index].Kind != SslTokenKind.Directive)
			{
				return String.Empty;
			}

			return this.GetText(index).TrimStart('#').Trim().ToLowerInvariant();
		}

		/// <summary>Finds the first token that starts at or after an offset.</summary>
		/// <param name="offset">The offset.</param>
		/// <returns>The index; <see cref="Count"/> when no token starts there or later.</returns>
		public Int32 FindFirstTokenFrom(Int32 offset)
		{
			Int32 low = 0;
			Int32 high = this._tokens.Length;
			while (low < high)
			{
				Int32 middle = low + ((high - low) / 2);
				if (this._tokens[middle].Offset < offset)
				{
					low = middle + 1;
				}
				else
				{
					high = middle;
				}
			}

			return low;
		}

		/// <summary>Finds the first token that ends after an offset (the token that contains the character at the offset, or the next one).</summary>
		/// <param name="offset">The offset.</param>
		/// <returns>The index; <see cref="Count"/> when there is none.</returns>
		public Int32 FindTokenAt(Int32 offset)
		{
			Int32 low = 0;
			Int32 high = this._tokens.Length;
			while (low < high)
			{
				Int32 middle = low + ((high - low) / 2);
				if (this._tokens[middle].End <= offset)
				{
					low = middle + 1;
				}
				else
				{
					high = middle;
				}
			}

			return low;
		}

		/// <summary>Gets the 1-based column of an offset.</summary>
		/// <param name="offset">The offset.</param>
		/// <returns>The column.</returns>
		public Int32 GetColumn(Int32 offset)
		{
			Int32 clamped = Math.Clamp(offset, 0, this.Text.Length);
			Int32 start = clamped;
			while (start > 0 && this.Text[start - 1] != '\n' && this.Text[start - 1] != '\r')
			{
				start--;
			}

			return clamped - start + 1;
		}
	}
}