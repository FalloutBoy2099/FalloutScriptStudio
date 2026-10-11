namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;

	/// <summary>Small hand-written scripts used with the real tools. Line numbers are part of the tests, so edit with care.</summary>
	internal static class SslSamples
	{
		/// <summary>A header with a macro and a procedure.</summary>
		public static readonly String LibraryHeader = Join(
			"#define LIB_CONSTANT 7",
			"procedure lib_helper(variable x) begin",
			"   return x * 2;",
			"end");

		/// <summary>A valid script with variables of every kind, forward declarations, references and an include.</summary>
		public static readonly String Basic = Join(
			"#include \"lib.h\"",
			"",
			"variable g_counter := 5;",
			"variable g_name := \"hello\";",
			"variable g_ratio := 1.5;",
			"variable g_plain;",
			"export variable g_exported;",
			"",
			"procedure start;",
			"procedure add_values(variable a, variable b);",
			"procedure timed_proc;",
			"",
			"procedure start begin",
			"   variable local_one := 10;",
			"   variable local_two;",
			"   g_counter := add_values(local_one, 4);",
			"   g_plain := g_counter + 1 + LIB_CONSTANT;",
			"   display_msg(g_name);",
			"end",
			"",
			"procedure add_values(variable a, variable b) begin",
			"   return a + b + g_counter + lib_helper(a);",
			"end",
			"",
			"procedure timed_proc begin",
			"   g_counter += 1;",
			"end");

		/// <summary>A script with a syntax error on line 3.</summary>
		public static readonly String SyntaxError = Join(
			"procedure start begin",
			"   variable x := ;",
			"   foo(1;",
			"end");

		/// <summary>A script that is syntactically valid but uses an undefined symbol on line 3.</summary>
		public static readonly String SemanticError = Join(
			"procedure start begin",
			"   variable a := 1;",
			"   a := b;",
			"end");

		/// <summary>Joins lines with the Windows line break used by Fallout scripts.</summary>
		public static String Join(params String[] lines)
		{
			return String.Join("\r\n", lines) + "\r\n";
		}
	}
}
