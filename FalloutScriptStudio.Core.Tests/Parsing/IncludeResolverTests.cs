namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class IncludeResolverTests
	{
		[Fact]
		public void Resolve_PrefersScriptDirectoryThenHeadersThenProgramDirectory()
		{
			using var root = new TempDirectory();
			String scripts = Path.Combine(root.Path, "scripts");
			String headers = Path.Combine(root.Path, "headers");
			String program = Path.Combine(root.Path, "program");
			root.Write(Path.Combine("scripts", "local.h"), String.Empty);
			root.Write(Path.Combine("headers", "local.h"), String.Empty);
			root.Write(Path.Combine("headers", "shared.h"), String.Empty);
			root.Write(Path.Combine("program", "app.h"), String.Empty);
			var resolver = new IncludeResolver(headers, program);

			Assert.Equal(Path.Combine(scripts, "local.h"), resolver.Resolve("local.h", scripts));
			Assert.Equal(Path.Combine(headers, "shared.h"), resolver.Resolve("shared.h", scripts));
			Assert.Equal(Path.Combine(program, "app.h"), resolver.Resolve("app.h", scripts));
		}

		[Fact]
		public void Resolve_WithoutHeadersDirectory_DoesNotSearchHeaders()
		{
			using var root = new TempDirectory();
			root.Write(Path.Combine("headers", "shared.h"), String.Empty);
			String scripts = Path.Combine(root.Path, "scripts");
			var resolver = new IncludeResolver(null, null);

			String resolved = resolver.Resolve("shared.h", scripts);

			Assert.Equal(Path.Combine(scripts, "shared.h"), resolved);
		}

		[Fact]
		public void Resolve_UnknownFile_FallsBackToScriptRelativePath()
		{
			using var root = new TempDirectory();
			var resolver = new IncludeResolver(Path.Combine(root.Path, "headers"), Path.Combine(root.Path, "program"));

			String resolved = resolver.Resolve(@"..\headers\none.h", Path.Combine(root.Path, "scripts"));

			Assert.Equal(Path.Combine(root.Path, "headers", "none.h"), resolved);
		}

		[Fact]
		public void GetAllIncludes_ListsResolvedIncludesAndIgnoresCommentedOnes()
		{
			using var root = new TempDirectory();
			String scriptPath = Path.Combine(root.Path, "a.ssl");
			String text = SslText.Lines(
				"#include \"one.h\"",
				"  #INCLUDE \"two.h\"  // trailing",
				"// #include \"three.h\"",
				"/*",
				"#include \"four.h\"",
				"*/",
				"#include broken");
			var resolver = new IncludeResolver(null, null);

			IReadOnlyList<String> includes = resolver.GetAllIncludes(text, scriptPath);

			Assert.Equal(new[] { Path.Combine(root.Path, "one.h"), Path.Combine(root.Path, "two.h") }, includes);
		}

		[Fact]
		public void ExtractIncludePath_RejectsLinesWithoutQuotes()
		{
			Assert.Equal("a\\b.h", IncludeResolver.ExtractIncludePath("#include \"a\\b.h\""));
			Assert.Null(IncludeResolver.ExtractIncludePath("#include <stdio.h>"));
		}
	}
}