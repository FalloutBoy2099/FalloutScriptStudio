namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;

	/// <summary>Builds script texts for the tests.</summary>
	internal static class SslText
	{
		/// <summary>Joins lines with CRLF like a Windows script file.</summary>
		public static String Lines(params String[] lines)
		{
			return String.Join("\r\n", lines);
		}

		/// <summary>A small realistic script: header, includes, forward declarations, two procedures.</summary>
		public static String BasicScript()
		{
			return Lines(
				"// Test script",                  // 1
				"#include \"define.h\"",           // 2
				"#define NAME SCRIPT_TEST",        // 3
				"",                                // 4
				"procedure start;",                // 5
				"procedure Talk_p_proc;",          // 6
				"",                                // 7
				"procedure start begin",           // 8
				"\tcall Talk_p_proc;",             // 9
				"end",                             // 10
				"",                                // 11
				"procedure Talk_p_proc begin",     // 12
				"\tdisplay_msg(\"hi\");",          // 13
				"end");                            // 14
		}
	}
}
