namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using IO;

	/// <summary>
	/// Joins a lone <c>begin</c> line to the <c>procedure</c> line above it
	/// (legacy <c>ParserInternal.FixProcedureBegin</c>), the layout the native parser expects.
	/// </summary>
	public sealed class ProcedureBeginFixer
	{
		/// <summary>
		/// Fixes the procedures of a text.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <returns>The fixed text. Line terminators of the input are preserved.</returns>
		public String FixText(String text)
		{
			String eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
			var lines = new List<String>(text.Split(eol));

			for (Int32 i = 0; i < lines.Count - 1; i++)
			{
				Boolean isHeader = lines[i].StartsWith(SslKeywords.Procedure, StringComparison.OrdinalIgnoreCase)
					|| lines[i].StartsWith("critical ", StringComparison.OrdinalIgnoreCase);

				if (isHeader && lines[i + 1].StartsWith(SslKeywords.Begin, StringComparison.OrdinalIgnoreCase))
				{
					lines[i] += ' ' + lines[i + 1];
					lines.RemoveAt(i + 1);
				}
			}

			return String.Join(eol, lines);
		}

		/// <summary>
		/// Fixes a script file in place (read and written with the Windows ANSI code page like the legacy editor).
		/// </summary>
		/// <param name="filePath">Path of the script.</param>
		/// <returns><c>true</c> when the file was changed.</returns>
		public Boolean FixFile(String filePath)
		{
			String original = File.ReadAllText(filePath, TextEncodings.Ansi);
			String fixedText = this.FixText(original);
			if (String.Equals(original, fixedText, StringComparison.Ordinal))
			{
				return false;
			}

			File.WriteAllText(filePath, fixedText, TextEncodings.Ansi);
			return true;
		}
	}
}