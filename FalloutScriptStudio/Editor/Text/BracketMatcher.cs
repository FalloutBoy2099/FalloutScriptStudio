namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Finds matching <c>()</c>, <c>[]</c> and <c>{}</c> pairs, ignoring brackets inside comments and strings.</summary>
	public static class BracketMatcher
	{
		private const String Openers = "([{";
		private const String Closers = ")]}";

		/// <summary>The number of characters copied out of the text at once while searching (a character by character read of a big document is slow).</summary>
		private const Int32 ChunkLength = 4096;

		/// <summary>
		/// Finds the pair for the bracket next to the caret: the character before the caret is examined first, then the
		/// one after it.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="map">The lexical map of the text.</param>
		/// <param name="caret">The caret offset.</param>
		/// <returns>The pair, or <see langword="null"/> when there is no bracket next to the caret or it has no partner.</returns>
		public static BracketPair? FindPair(ITextSource text, LexicalMap map, Int32 caret)
		{
			return FindPair(text, () => map, caret);
		}

		/// <summary>Finds the pair for the bracket next to the caret; the lexical map is requested only when a bracket is there.</summary>
		/// <param name="text">The text.</param>
		/// <param name="getMap">Returns the lexical map of the text.</param>
		/// <param name="caret">The caret offset.</param>
		/// <returns>The pair, or <see langword="null"/>.</returns>
		public static BracketPair? FindPair(ITextSource text, Func<LexicalMap> getMap, Int32 caret)
		{
			BracketPair? pair = caret > 0 ? FindPairOf(text, getMap, caret - 1) : null;
			return pair ?? (caret < text.TextLength ? FindPairOf(text, getMap, caret) : null);
		}

		/// <summary>Finds the partner of the bracket at an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="map">The lexical map of the text.</param>
		/// <param name="offset">Offset of a bracket character.</param>
		/// <returns>The pair, or <see langword="null"/> when the character is not a code bracket or has no partner.</returns>
		public static BracketPair? FindPairOf(ITextSource text, LexicalMap map, Int32 offset)
		{
			return FindPairOf(text, () => map, offset);
		}

		private static BracketPair? FindPairOf(ITextSource text, Func<LexicalMap> getMap, Int32 offset)
		{
			if (offset < 0 || offset >= text.TextLength)
			{
				return null;
			}

			Char c = text.GetCharAt(offset);
			Int32 opener = Openers.IndexOf(c);
			Int32 closer = Closers.IndexOf(c);
			if (opener < 0 && closer < 0)
			{
				return null;
			}

			LexicalMap map = getMap();
			if (!IsBracketPosition(map, offset))
			{
				return null;
			}

			if (opener >= 0)
			{
				Char close = Closers[opener];
				Int32 depth = 0;
				for (Int32 chunkStart = offset; chunkStart < text.TextLength; chunkStart += ChunkLength)
				{
					String chunk = text.GetText(chunkStart, Math.Min(ChunkLength, text.TextLength - chunkStart));
					for (Int32 k = 0; k < chunk.Length; k++)
					{
						Char ch = chunk[k];
						if (ch == c && IsBracketPosition(map, chunkStart + k))
						{
							depth++;
						}
						else if (ch == close && IsBracketPosition(map, chunkStart + k) && --depth == 0)
						{
							return new BracketPair(offset, chunkStart + k);
						}
					}
				}

				return null;
			}

			Char open = Openers[closer];
			Int32 level = 0;
			for (Int32 chunkEnd = offset + 1; chunkEnd > 0; chunkEnd -= ChunkLength)
			{
				Int32 chunkStart = Math.Max(0, chunkEnd - ChunkLength);
				String chunk = text.GetText(chunkStart, chunkEnd - chunkStart);
				for (Int32 k = chunk.Length - 1; k >= 0; k--)
				{
					Char ch = chunk[k];
					if (ch == c && IsBracketPosition(map, chunkStart + k))
					{
						level++;
					}
					else if (ch == open && IsBracketPosition(map, chunkStart + k) && --level == 0)
					{
						return new BracketPair(chunkStart + k, offset);
					}
				}
			}

			return null;
		}

		private static Boolean IsBracketPosition(LexicalMap map, Int32 offset)
		{
			LexicalKind kind = map.GetKindAt(offset);
			return kind == LexicalKind.Code || kind == LexicalKind.Braces;
		}
	}
}