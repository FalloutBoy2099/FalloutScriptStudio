namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;

	/// <summary>Script texts and helpers shared by the text tools, refactoring and dialogue tests.</summary>
	internal static class TestScripts
	{
		/// <summary>Joins lines with CRLF like a Windows script file.</summary>
		public static String Crlf(params String[] lines)
		{
			return String.Join("\r\n", lines);
		}

		/// <summary>A small dialogue script with forward declarations (every line is numbered in the comments of the source).</summary>
		public static String DialogueScript()
		{
			return Crlf(
				"// Test script",                        // 1
				"#include \"define.h\"",                 // 2
				"#define NAME SCRIPT_TEST",              // 3
				"",                                      // 4
				"procedure start;",                      // 5
				"procedure talk_p_proc;",                // 6
				"procedure Node001;",                    // 7
				"procedure Node999;",                    // 8
				"",                                      // 9
				"procedure start begin",                 // 10
				"end",                                   // 11
				"",                                      // 12
				"procedure talk_p_proc begin",           // 13
				"   Reply(100);",                        // 14
				"   NOption(101, Node001, 4);",          // 15
				"   NOption(102, Node999, 4);",          // 16
				"end",                                   // 17
				"",                                      // 18
				"procedure Node001 begin",               // 19
				"   Reply(103);",                        // 20
				"   NOption(104, Node999, 4);",          // 21
				"end",                                   // 22
				"",                                      // 23
				"procedure Node999 begin",               // 24
				"end");                                  // 25
		}
	}
}
