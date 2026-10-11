namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Determines whether a position of a script lies in a comment or a string. It replaces the legacy check of the syntax
	/// highlighter colour at the caret (<c>ColorTheme.CheckColorPosition</c>) with a plain text scan.
	/// </summary>
	public static class SslLexicalScanner
	{
		/// <summary>Gets the context of a position.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="offset">The offset of the caret: the context is the one of the character just before it.</param>
		/// <returns>The context at the offset.</returns>
		public static LexicalContext GetContext(String text, Int32 offset)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 limit = Math.Clamp(offset, 0, text.Length);
			LexicalContext context = LexicalContext.Code;
			Int32 i = 0;
			while (i < limit)
			{
				Char c = text[i];
				switch (context)
				{
					case LexicalContext.Code:
						if (c == '"')
						{
							context = LexicalContext.StringLiteral;
						}
						else if (c == '/' && i + 1 < limit && text[i + 1] == '/')
						{
							context = LexicalContext.LineComment;
							i++;
						}
						else if (c == '/' && i + 1 < limit && text[i + 1] == '*')
						{
							context = LexicalContext.BlockComment;
							i++;
						}

						break;
					case LexicalContext.StringLiteral:
						if (c == '"' || c == '\n')
						{
							context = LexicalContext.Code;
						}

						break;
					case LexicalContext.LineComment:
						if (c == '\n')
						{
							context = LexicalContext.Code;
						}

						break;
					case LexicalContext.BlockComment:
						if (c == '*' && i + 1 < limit && text[i + 1] == '/')
						{
							context = LexicalContext.Code;
							i++;
						}

						break;
				}

				i++;
			}

			return context;
		}

		/// <summary>Determines whether a position is in a comment or a string.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="offset">The offset of the caret.</param>
		/// <returns><c>true</c> when the position is not ordinary code.</returns>
		public static Boolean IsInCommentOrString(String text, Int32 offset)
		{
			return GetContext(text, offset) != LexicalContext.Code;
		}
	}
}
