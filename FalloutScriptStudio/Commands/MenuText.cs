namespace FalloutScriptStudio.Commands
{
	using System;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>Turns the header of a menu item into the plain text the command palette shows: no access key marker and no trailing ellipsis.</summary>
	public static partial class MenuText
	{
		/// <summary>
		/// Removes the access key marker (<c>F_ile</c>, and the Chinese form <c>保存(_S)</c>) and the ellipsis at the end (<c>Open...</c>).
		/// </summary>
		/// <param name="header">The text of a menu header.</param>
		/// <returns>The plain text.</returns>
		public static String Clean(String header)
		{
			ArgumentNullException.ThrowIfNull(header);

			String text = ChineseAccessKey().Replace(header, String.Empty);
			StringBuilder builder = new(text.Length);
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] == '_')
				{
					// "__" is a literal underscore, a single one marks the access key of the next character.
					if (i + 1 < text.Length && text[i + 1] == '_')
					{
						builder.Append('_');
						i++;
					}

					continue;
				}

				builder.Append(text[i]);
			}

			String result = builder.ToString().TrimEnd();
			while (result.EndsWith("...", StringComparison.Ordinal) || result.EndsWith('…'))
			{
				result = result.EndsWith('…') ? result[..^1] : result[..^3];
				result = result.TrimEnd();
			}

			return result;
		}

		[GeneratedRegex(@"\(_[^)]\)")]
		private static partial Regex ChineseAccessKey();
	}
}