namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;

	/// <summary>The arguments of one call found in a (masked) text: where each starts and ends.</summary>
	internal sealed class MessageCallArguments
	{
		private readonly String _text;
		private readonly List<(Int32 Start, Int32 End)> _spans;

		private MessageCallArguments(String text, List<(Int32 Start, Int32 End)> spans, Int32 end)
		{
			this._text = text;
			this._spans = spans;
			this.End = end;
		}

		/// <summary>Gets the number of arguments.</summary>
		public Int32 Count => this._spans.Count;

		/// <summary>Gets the offset after the closing parenthesis.</summary>
		public Int32 End { get; }

		/// <summary>Gets the text of an argument, without the white space around it.</summary>
		/// <param name="index">The zero-based position.</param>
		/// <returns>The text.</returns>
		public String this[Int32 index] => this._text.Substring(this._spans[index].Start, this._spans[index].End - this._spans[index].Start);

		/// <summary>Gets the offset of the first character of an argument.</summary>
		/// <param name="index">The zero-based position.</param>
		/// <returns>The offset in the text.</returns>
		public Int32 StartOf(Int32 index)
		{
			return this._spans[index].Start;
		}

		/// <summary>Reads the arguments of the call whose opening parenthesis is at <paramref name="open"/>.</summary>
		/// <param name="text">The masked text.</param>
		/// <param name="open">The offset of the <c>(</c>.</param>
		/// <returns>The arguments, or <see langword="null"/> when the parenthesis is never closed.</returns>
		public static MessageCallArguments? Parse(String text, Int32 open)
		{
			List<(Int32 Start, Int32 End)> spans = [];
			Int32 depth = 1;
			Int32 start = open + 1;
			for (Int32 i = open + 1; i < text.Length; i++)
			{
				Char c = text[i];
				if (c == '(')
				{
					depth++;
				}
				else if (c == ')' || (c == ',' && depth == 1))
				{
					if (c == ')' && --depth > 0)
					{
						continue;
					}

					spans.Add(Trim(text, start, i));
					start = i + 1;
					if (c == ')')
					{
						// A call without arguments, "f()", has one empty span; it is no argument.
						if (spans.Count == 1 && spans[0].Start == spans[0].End)
						{
							spans.Clear();
						}

						return new MessageCallArguments(text, spans, i + 1);
					}
				}
			}

			return null;
		}

		private static (Int32 Start, Int32 End) Trim(String text, Int32 start, Int32 end)
		{
			while (start < end && Char.IsWhiteSpace(text[start]))
			{
				start++;
			}

			while (end > start && Char.IsWhiteSpace(text[end - 1]))
			{
				end--;
			}

			return (start, end);
		}
	}
}