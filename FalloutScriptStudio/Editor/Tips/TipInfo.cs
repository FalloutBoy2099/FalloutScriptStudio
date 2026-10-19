namespace FalloutScriptStudio.Editor.Tips
{
	using System;

	/// <summary>The content of a tooltip: a highlighted title line and a description.</summary>
	/// <param name="Title">The first line (signature or name).</param>
	/// <param name="Body">The remaining text; empty when there is none.</param>
	/// <param name="Kind">What the tooltip describes.</param>
	public sealed record TipInfo(String Title, String Body, TipKind Kind)
	{
		/// <summary>Splits a description at its first line break.</summary>
		/// <param name="description">The description text.</param>
		/// <param name="kind">What the tooltip describes.</param>
		/// <returns>The tooltip.</returns>
		public static TipInfo FromDescription(String description, TipKind kind)
		{
			String text = description.Replace("\r\n", "\n").Trim('\n');
			Int32 lineBreak = text.IndexOf('\n');
			if (lineBreak < 0)
			{
				return new TipInfo(text, String.Empty, kind);
			}

			return new TipInfo(text.Substring(0, lineBreak).TrimEnd(), text.Substring(lineBreak + 1).Trim('\n'), kind);
		}
	}
}