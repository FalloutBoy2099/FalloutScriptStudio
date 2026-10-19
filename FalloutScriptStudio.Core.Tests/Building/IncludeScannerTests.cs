namespace FalloutScriptStudio.Core.Tests.Building
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Building;
	using Xunit;

	public sealed class IncludeScannerTests
	{
		private static IReadOnlyList<IncludeDirective> Scan(params String[] lines)
		{
			return IncludeScanner.Scan(String.Join("\r\n", lines));
		}

		[Fact]
		public void Quoted_and_angle_includes_are_found_with_their_lines()
		{
			IReadOnlyList<IncludeDirective> directives = Scan("#include \"a.h\"", "", "#include <b.h>", "  #  include   \"sub\\c.h\"  // trailing comment");

			Assert.Equal(new[] { "a.h", "b.h", "sub\\c.h" }, directives.Select(item => item.Path));
			Assert.Equal(new[] { 1, 3, 4 }, directives.Select(item => item.Line));
			Assert.All(directives, item => Assert.False(item.Conditional));
		}

		[Fact]
		public void Comments_and_strings_do_not_hide_or_invent_includes()
		{
			IReadOnlyList<IncludeDirective> directives = Scan(
				"// #include \"line.h\"",
				"/* #include \"block.h\" */",
				"/*",
				"#include \"inside.h\"",
				"*/ #include \"after.h\"",
				"display_msg(\"/* not a comment\");",
				"#include \"real.h\"");

			Assert.Equal(new[] { "after.h", "real.h" }, directives.Select(item => item.Path));
		}

		[Fact]
		public void A_block_comment_opened_by_a_string_does_not_swallow_the_includes()
		{
			IReadOnlyList<IncludeDirective> directives = Scan("variable s = \"/*\";", "#include \"kept.h\"");

			// The text is not an SSL parser: a comment start inside a string literal is seen as a string, so the include stays visible.
			Assert.Equal("kept.h", Assert.Single(directives).Path);
		}

		[Fact]
		public void Includes_inside_conditional_blocks_are_marked()
		{
			IReadOnlyList<IncludeDirective> directives = Scan(
				"#include \"always.h\"",
				"#ifdef DEBUG",
				"#include \"debug.h\"",
				"#ifndef QUIET",
				"#include \"loud.h\"",
				"#endif",
				"#else",
				"#include \"release.h\"",
				"#endif",
				"#if 1",
				"#endif",
				"#include \"after.h\"",
				"#endif");

			Assert.Equal(new[] { false, true, true, true, false }, directives.Select(item => item.Conditional));
		}

		[Fact]
		public void An_include_that_does_not_name_a_file_literally_is_reported_without_a_path()
		{
			IReadOnlyList<IncludeDirective> directives = Scan("#include HEADER", "#include \"unterminated.h", "#include \"\"", "#include");

			Assert.Equal(4, directives.Count);
			Assert.All(directives, item => Assert.False(item.IsLiteral));
		}

		[Fact]
		public void Other_directives_and_plain_text_are_ignored()
		{
			IReadOnlyList<IncludeDirective> directives = Scan("#define INCLUDE_ME 1", "#pragma once", "procedure include_x begin end", "  include \"x.h\"");

			Assert.Empty(directives);
		}

		[Fact]
		public void Both_line_ending_styles_and_a_last_line_without_one_are_handled()
		{
			Assert.Equal(new[] { "a.h", "b.h", "c.h" }, IncludeScanner.Scan("#include \"a.h\"\n#include \"b.h\"\r\n#include \"c.h\"").Select(item => item.Path));
			Assert.Empty(IncludeScanner.Scan(String.Empty));
		}
	}
}