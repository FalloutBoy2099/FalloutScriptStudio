namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;
	using System.Globalization;

	/// <summary>
	/// One registered script: a line of <c>scripts.lst</c> together with its name in <c>scrname.msg</c>.
	/// The position of the entry in the list (1-based) is the script id.
	/// </summary>
	public sealed class ScriptListEntry
	{
		/// <summary>The marker that precedes the number of local variables.</summary>
		public const String LocalVarsMarker = "# local_vars=";

		/// <summary>The id of the first script in <c>scrname.msg</c>; message <c>101</c> is the name of script 1.</summary>
		public const Int32 FirstMessageId = 101;

		private const Int32 ScriptColumnWidth = 16;
		private const Int32 DescriptionColumnWidth = 45;
		private const Int32 MessageColumnWidth = 60;

		/// <summary>The Cyrillic capital Ha (U+0425), which looks like the Latin X.</summary>
		private const Char CyrillicHa = (Char)0x0425;

		/// <summary>Gets or sets the compiled script file name (<c>name.int</c>).</summary>
		public String Script { get; set; } = String.Empty;

		/// <summary>Gets or sets the description.</summary>
		public String Description { get; set; } = String.Empty;

		/// <summary>Gets or sets the number of local variables of the script.</summary>
		public Int32 LocalVars { get; set; }

		/// <summary>Gets or sets the name of the script in the game (<c>scrname.msg</c>).</summary>
		public String GameName { get; set; } = String.Empty;

		/// <summary>Gets or sets the sound (lip) field of the <c>scrname.msg</c> message.</summary>
		public String Lip { get; set; } = String.Empty;

		/// <summary>
		/// Parses a line of <c>scripts.lst</c> (<c>script.int      ; description      # local_vars=N</c>).
		/// Lines that do not follow the format are tolerated: missing parts are left empty.
		/// </summary>
		/// <param name="line">The line.</param>
		/// <returns>The entry.</returns>
		public static ScriptListEntry Parse(String line)
		{
			ArgumentNullException.ThrowIfNull(line);

			ScriptListEntry entry = new();
			Int32 varPos = line.LastIndexOf(LocalVarsMarker, StringComparison.Ordinal);
			Int32 descPos = line.IndexOf(';');
			if (varPos >= 0 && descPos > varPos)
			{
				descPos = -1;
			}

			Int32 scriptEnd = descPos >= 0 ? descPos : (varPos >= 0 ? varPos : line.Length);
			entry.Script = line.Substring(0, scriptEnd).Trim();
			if (descPos >= 0)
			{
				Int32 descEnd = varPos >= 0 ? varPos : line.Length;
				entry.Description = line.Substring(descPos + 1, descEnd - descPos - 1).Trim();
			}

			if (varPos >= 0)
			{
				entry.LocalVars = ParseCount(line.Substring(varPos + LocalVarsMarker.Length));
			}

			return entry;
		}

		/// <summary>Formats the entry as a line of <c>scripts.lst</c>.</summary>
		/// <returns>The line.</returns>
		public String ToListLine()
		{
			return this.Script.PadRight(ScriptColumnWidth) + "; " + this.Description.PadRight(DescriptionColumnWidth) + " " + LocalVarsMarker + this.LocalVars.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Formats the entry as a line of <c>scrname.msg</c>.</summary>
		/// <param name="index">The 0-based position of the entry in the list.</param>
		/// <param name="replaceCyrillicHa">Replace the Cyrillic capital Ha (which looks like the Latin X) in the name; needed for cp866 files.</param>
		/// <returns>The line.</returns>
		public String ToMessageLine(Int32 index, Boolean replaceCyrillicHa)
		{
			String name = replaceCyrillicHa ? this.GameName.Replace(CyrillicHa, 'X') : this.GameName;
			String head = "{" + (index + FirstMessageId).ToString(CultureInfo.InvariantCulture) + "}{" + this.Lip + "}{" + name + "}";
			return head.PadRight(MessageColumnWidth) + "# " + this.Script.PadRight(ScriptColumnWidth) + "; " + this.Description;
		}

		private static Int32 ParseCount(String text)
		{
			String trimmed = text.Trim();
			if (Int32.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 value))
			{
				return value;
			}

			Int32 length = 0;
			while (length < trimmed.Length && trimmed[length] >= '0' && trimmed[length] <= '9')
			{
				length++;
			}

			return length > 0 && Int32.TryParse(trimmed.AsSpan(0, length), NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value : 0;
		}
	}
}