namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;

	/// <summary>Builds script texts for the analysis tests.</summary>
	internal static class Ssl
	{
		/// <summary>Joins lines with CRLF like a Windows script file.</summary>
		public static String Crlf(params String[] lines)
		{
			return String.Join("\r\n", lines);
		}

		/// <summary>Joins lines with LF.</summary>
		public static String Lf(params String[] lines)
		{
			return String.Join("\n", lines);
		}

		/// <summary>Changes the line breaks of a text to LF.</summary>
		public static String ToLf(String text)
		{
			return text.Replace("\r\n", "\n", StringComparison.Ordinal);
		}
	}
}