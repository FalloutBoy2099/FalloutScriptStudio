namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Tracks block comments (<c>/* ... */</c>) while a script is scanned line by line
	/// (legacy <c>ParserInternal.CommentBlockParse</c>). Create one instance per scan.
	/// </summary>
	public sealed class CommentTracker
	{
		private Int32 _depth;

		/// <summary>Gets a value indicating whether the scan is currently inside a block comment.</summary>
		public Boolean IsInsideBlockComment => this._depth > 0;

		/// <summary>
		/// Processes the next line of the script.
		/// </summary>
		/// <param name="line">
		/// The line. A block comment that is fully contained in the line is removed; a comment that starts in the line
		/// is cut off; a line inside a comment is cleared.
		/// </param>
		/// <returns>
		/// <c>true</c> when the whole line must be skipped (empty or one-character line, <c>//</c> comment,
		/// or a line that is entirely inside a block comment); otherwise <c>false</c> and <paramref name="line"/>
		/// contains the code part of the line.
		/// </returns>
		public Boolean ShouldSkip(ref String line)
		{
			if (line.Length < 2 || line.StartsWith(SslKeywords.LineComment, StringComparison.Ordinal))
			{
				return true;
			}

			if (this._depth == 0)
			{
				Int32 start = line.IndexOf(SslKeywords.BlockCommentStart, StringComparison.Ordinal);
				if (start != -1)
				{
					Int32 end = line.IndexOf(SslKeywords.BlockCommentEnd, start + 2, StringComparison.Ordinal);
					if (end < 0)
					{
						this._depth++;
						line = line.Remove(start);
						return true;
					}

					line = line.Remove(start, end + 2 - start).Insert(start, " ").TrimStart();
				}
			}
			else
			{
				Int32 end = line.IndexOf(SslKeywords.BlockCommentEnd, StringComparison.Ordinal);
				if (end == -1)
				{
					line = String.Empty;
					return true;
				}

				this._depth--;
				if (line.Length > 2)
				{
					line = line.Remove(0, end + 2).TrimStart();
				}
			}

			return false;
		}
	}
}
