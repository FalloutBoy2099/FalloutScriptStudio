namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class ScriptEnvironmentBuilderTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("diagnostics-includes");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		private ScriptEnvironmentBuilder CreateBuilder(IncludeSymbolCache? cache = null, Func<String, String?>? overlay = null, Func<String, String?>? locate = null, String? headers = null)
		{
			return new ScriptEnvironmentBuilder(new IncludeResolver(headers, null), cache ?? new IncludeSymbolCache(), File.ReadAllText, overlay, locate);
		}

		private String Old(String relativePath, String content)
		{
			String path = this._folder.Write(relativePath, content);
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10));
			return path;
		}

		[Fact]
		public void The_procedures_and_variables_of_the_included_headers_are_collected_through_nested_includes()
		{
			this.Old("lib\\inner.h", "variable inner_var;\r\nprocedure inner_proc(variable a, variable b := 1) begin end\r\n");
			this.Old("lib\\outer.h", "#include \"inner.h\"\r\nprocedure outer_proc;\r\nexport variable outer_var;\r\n");
			String script = this.Old("main.ssl", "#include \"lib\\outer.h\"\r\nprocedure start begin end\r\n");
			SslTokenStream tokens = SslTokenizer.Tokenize(File.ReadAllText(script));

			ScriptEnvironment environment = this.CreateBuilder().Build(tokens, script, null, null);

			Assert.True(environment.IsComplete);
			Assert.True(environment.Symbols.TryGetProcedure("INNER_PROC", out ExternalProcedure? inner));
			Assert.Equal((1, 2, true), (inner!.MinArguments, inner.MaxArguments, inner.HasBody));
			Assert.True(environment.Symbols.TryGetProcedure("outer_proc", out ExternalProcedure? outer));
			Assert.False(outer!.HasBody);
			Assert.True(environment.Symbols.ContainsVariable("inner_var"));
			Assert.True(environment.Symbols.ContainsVariable("OUTER_VAR"));
		}

		[Fact]
		public void A_header_that_cannot_be_found_makes_the_environment_incomplete()
		{
			String script = this.Old("main.ssl", "#include \"missing.h\"\r\nprocedure start begin end\r\n");

			ScriptEnvironment environment = this.CreateBuilder().Build(SslTokenizer.Tokenize(File.ReadAllText(script)), script, null, null);

			Assert.False(environment.IsComplete);
		}

		[Fact]
		public void A_header_is_found_in_the_headers_folder_and_through_the_locator()
		{
			String headers = Path.Combine(this._folder.Path, "headers");
			this.Old("headers\\global.h", "procedure global_proc;\r\n");
			String elsewhere = this.Old("elsewhere\\extra.h", "variable extra_var;\r\n");
			String script = this.Old("main.ssl", "#include \"global.h\"\r\n#include \"extra.h\"\r\nprocedure start begin end\r\n");
			SslTokenStream tokens = SslTokenizer.Tokenize(File.ReadAllText(script));

			ScriptEnvironment withoutLocator = this.CreateBuilder(headers: headers).Build(tokens, script, null, null);
			ScriptEnvironment withLocator = this.CreateBuilder(headers: headers, locate: include => String.Equals(include, "extra.h", StringComparison.Ordinal) ? elsewhere : null).Build(tokens, script, null, null);

			Assert.False(withoutLocator.IsComplete);
			Assert.True(withoutLocator.Symbols.TryGetProcedure("global_proc", out _));
			Assert.True(withLocator.IsComplete);
			Assert.True(withLocator.Symbols.ContainsVariable("extra_var"));
		}

		[Fact]
		public void A_header_whose_macros_may_declare_names_makes_the_environment_incomplete()
		{
			this.Old("macros.h", "#define MAKE(n) procedure n begin end\r\nMAKE(generated)\r\n");
			String script = this.Old("main.ssl", "#include \"macros.h\"\r\nprocedure start begin end\r\n");

			ScriptEnvironment environment = this.CreateBuilder().Build(SslTokenizer.Tokenize(File.ReadAllText(script)), script, null, null);

			Assert.False(environment.IsComplete);
		}

		[Fact]
		public void A_circular_include_is_followed_once()
		{
			this.Old("a.h", "#include \"b.h\"\r\nprocedure from_a;\r\n");
			this.Old("b.h", "#include \"a.h\"\r\nprocedure from_b;\r\n");
			String script = this.Old("main.ssl", "#include \"a.h\"\r\nprocedure start begin end\r\n");

			ScriptEnvironment environment = this.CreateBuilder().Build(SslTokenizer.Tokenize(File.ReadAllText(script)), script, null, null);

			Assert.True(environment.IsComplete);
			Assert.Equal(2, environment.Symbols.ProcedureCount);
		}

		[Fact]
		public void The_text_of_an_open_document_with_unsaved_changes_replaces_the_file()
		{
			String header = this.Old("lib.h", "procedure on_disk;\r\n");
			String script = this.Old("main.ssl", "#include \"lib.h\"\r\nprocedure start begin end\r\n");
			Func<String, String?> overlay = path => String.Equals(path, header, StringComparison.OrdinalIgnoreCase) ? "procedure unsaved;\r\n" : null;

			ScriptEnvironment environment = this.CreateBuilder(overlay: overlay).Build(SslTokenizer.Tokenize(File.ReadAllText(script)), script, null, null);

			Assert.True(environment.Symbols.TryGetProcedure("unsaved", out _));
			Assert.False(environment.Symbols.TryGetProcedure("on_disk", out _));
		}

		[Fact]
		public void The_macros_and_the_functions_are_handed_to_the_environment()
		{
			String script = this.Old("main.ssl", "procedure start begin end\r\n");
			ScriptMacro macro = new ScriptMacro("LIMIT", "LIMIT", "10", "h.h", 1, null);
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Empty;

			ScriptEnvironment environment = this.CreateBuilder().Build(SslTokenizer.Tokenize(File.ReadAllText(script)), script, name => name == "LIMIT" ? macro : null, catalog);

			Assert.Same(macro, environment.FindMacro!("LIMIT"));
			Assert.Null(environment.FindMacro!("other"));
			Assert.Same(catalog, environment.Functions);
		}

		[Fact]
		public void The_include_directives_are_listed_in_order_with_quotes_and_angle_brackets()
		{
			SslTokenStream tokens = SslTokenizer.Tokenize("#include \"a.h\"\r\n# include <b\\c.h>\r\n// #include \"commented.h\"\r\n/* #include \"block.h\" */\r\n#define X \"d.h\"\r\n#include \"e.h\"\r\n");

			IReadOnlyList<String> includes = ScriptDiagnostics.FindIncludes(tokens);

			Assert.Equal(new[] { "a.h", "b\\c.h", "e.h" }, includes);
		}
	}

	public class IncludeSymbolCacheTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("diagnostics-cache");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		[Fact]
		public void A_file_is_read_once_while_it_does_not_change()
		{
			String path = this._folder.Write("h.h", "procedure first;\r\n");
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10));
			IncludeSymbolCache cache = new IncludeSymbolCache();
			Int32 reads = 0;
			Func<String, String?> read = p =>
			{
				reads++;
				return File.ReadAllText(p);
			};

			ScriptSymbolTable? one = cache.GetOrRead(path, read);
			ScriptSymbolTable? two = cache.GetOrRead(path, read);

			Assert.Same(one, two);
			Assert.Equal(1, reads);
			Assert.Equal(1, cache.Count);
		}

		[Fact]
		public void A_file_that_changed_is_read_again()
		{
			String path = this._folder.Write("h.h", "procedure first;\r\n");
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10));
			IncludeSymbolCache cache = new IncludeSymbolCache();
			cache.GetOrRead(path, File.ReadAllText);

			File.WriteAllText(path, "procedure second;\r\nprocedure third;\r\n");
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
			ScriptSymbolTable? table = cache.GetOrRead(path, File.ReadAllText);

			Assert.Equal(new[] { "second", "third" }, table!.Procedures.Select(p => p.Name));
		}

		[Fact]
		public void A_file_written_a_moment_ago_is_never_cached()
		{
			String path = this._folder.Write("h.h", "procedure first;\r\n");
			IncludeSymbolCache cache = new IncludeSymbolCache();

			cache.GetOrRead(path, File.ReadAllText);

			Assert.Equal(0, cache.Count);
		}

		[Fact]
		public void A_missing_or_unreadable_file_gives_nothing()
		{
			IncludeSymbolCache cache = new IncludeSymbolCache();
			String path = this._folder.Write("h.h", "procedure first;\r\n");

			Assert.Null(cache.GetOrRead(this._folder.Combine("missing.h"), File.ReadAllText));
			Assert.Null(cache.GetOrRead(path, _ => null));
		}

		[Fact]
		public void Clear_forgets_everything()
		{
			String path = this._folder.Write("h.h", "procedure first;\r\n");
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10));
			IncludeSymbolCache cache = new IncludeSymbolCache();
			cache.GetOrRead(path, File.ReadAllText);

			cache.Clear();

			Assert.Equal(0, cache.Count);
		}
	}

	public class ScriptSymbolTableTests
	{
		[Fact]
		public void The_table_lists_procedures_with_their_argument_range_variables_and_includes()
		{
			ScriptSymbolTable table = ScriptSymbolTable.Extract(Ssl.Crlf(
				"#include \"a.h\"",
				"variable v1, v2 := 3;",
				"export variable ev;",
				"procedure fwd(variable a, variable b);",
				"procedure body(variable a, variable b := 2) begin",
				"\tvariable local;",
				"end",
				"import procedure imported;"));

			Assert.Equal(new[] { "v1", "v2", "ev" }, table.Variables);
			Assert.Equal(new[] { "a.h" }, table.Includes);
			Assert.False(table.IsOpaque);
			ScriptProcedureSymbol forward = Assert.Single(table.Procedures, p => p.Name == "fwd");
			Assert.Equal((2, 2, false), (forward.MinArguments, forward.MaxArguments, forward.HasBody));
			ScriptProcedureSymbol body = Assert.Single(table.Procedures, p => p.Name == "body");
			Assert.Equal((1, 2, true), (body.MinArguments, body.MaxArguments, body.HasBody));
			Assert.False(Assert.Single(table.Procedures, p => p.Name == "imported").HasBody);
		}

		[Fact]
		public void A_forward_declaration_and_the_definition_become_one_symbol_with_a_body()
		{
			ScriptSymbolTable table = ScriptSymbolTable.Extract("procedure foo(variable a);\r\nprocedure foo(variable a) begin end\r\n");

			ScriptProcedureSymbol symbol = Assert.Single(table.Procedures);

			Assert.True(symbol.HasBody);
		}

		[Fact]
		public void Names_declared_in_the_branch_that_is_not_analysed_are_listed_too()
		{
			ScriptSymbolTable table = ScriptSymbolTable.Extract(Ssl.Crlf("#ifdef A", "variable a;", "#else", "variable b, c;", "procedure only_else;", "#endif"));

			Assert.Contains("a", table.Variables);
			Assert.Contains("b", table.Variables);
			Assert.Contains("c", table.Variables);
			Assert.Contains(table.Procedures, p => p.Name == "only_else");
		}

		[Fact]
		public void A_use_of_an_unknown_macro_at_the_top_level_makes_the_table_opaque()
		{
			Assert.True(ScriptSymbolTable.Extract("MAKE_THINGS(a, b)\r\nprocedure p;\r\n").IsOpaque);
			Assert.True(ScriptSymbolTable.Extract("#define DECL variable generated;\r\nDECL\r\n").IsOpaque);
			Assert.False(ScriptSymbolTable.Extract("#define PLAIN 5\r\nPLAIN\r\nprocedure p;\r\n").IsOpaque);
		}

		[Fact]
		public void The_empty_table_has_nothing()
		{
			Assert.Empty(ScriptSymbolTable.Empty.Procedures);
			Assert.Empty(ScriptSymbolTable.Empty.Variables);
			Assert.Empty(ScriptSymbolTable.Empty.Includes);
		}
	}

	public class ScriptBuiltinsTests
	{
		[Theory]
		[InlineData("display_msg")]
		[InlineData("DUDE_OBJ")]
		[InlineData("false")]
		[InlineData("bwand")]
		[InlineData("get_config_setting")]
		[InlineData("begin")]
		public void The_names_the_compiler_knows_are_builtin(String name)
		{
			Assert.True(ScriptBuiltins.IsBuiltin(name));
		}

		[Theory]
		[InlineData("my_procedure")]
		[InlineData("display_msgg")]
		[InlineData("")]
		public void Other_names_are_not(String name)
		{
			Assert.False(ScriptBuiltins.IsBuiltin(name));
		}

		[Theory]
		[InlineData("begin", true)]
		[InlineData("THEN", true)]
		[InlineData("bwand", true)]
		[InlineData("display_msg", false)]
		[InlineData("true", false)]
		public void Keywords_are_a_part_of_the_builtin_names(String name, Boolean keyword)
		{
			Assert.Equal(keyword, ScriptBuiltins.IsKeyword(name));
			Assert.True(ScriptBuiltins.IsBuiltin(name));
		}

		[Fact]
		public void The_shipped_descriptions_only_add_names_the_builtin_list_has_or_that_are_macros_of_the_headers()
		{
			Assert.True(ScriptBuiltins.Count > 600, "builtin names: " + ScriptBuiltins.Count);
			Assert.All(ScriptBuiltins.Names, name => Assert.Equal(name.ToLowerInvariant(), name));
		}
	}

	public class MacroTraitsTests
	{
		[Theory]
		[InlineData("5", true)]
		[InlineData("(5)", true)]
		[InlineData("\"text\"", true)]
		[InlineData("(1 + 2) * 3", true)]
		[InlineData("-1", true)]
		[InlineData("", false)]
		[InlineData("some_var", false)]
		[InlineData("(some_var + 1)", false)]
		[InlineData("1, 2", false)]
		public void A_body_of_literals_is_a_constant(String body, Boolean constant)
		{
			Assert.Equal(constant, MacroTraits.From(body).IsConstant);
		}

		[Fact]
		public void The_body_tells_if_it_declares_opens_a_block_spreads_arguments_or_pastes()
		{
			Assert.True(MacroTraits.From("variable tmp;").IsDeclaring);
			Assert.True(MacroTraits.From("procedure p begin end").IsDeclaring);
			Assert.True(MacroTraits.From("procedure p begin end").IsStructural);
			Assert.True(MacroTraits.From("then begin").IsStructural);
			Assert.True(MacroTraits.From("do").IsStructural);
			Assert.False(MacroTraits.From("display_msg(\"begin\")").IsStructural);
			Assert.True(MacroTraits.From("1, 2").SpreadsArguments);
			Assert.True(MacroTraits.From("foo(1))").SpreadsArguments);
			Assert.True(MacroTraits.From("(foo(1)").SpreadsArguments);
			Assert.False(MacroTraits.From("foo(1, 2)").SpreadsArguments);
			Assert.True(MacroTraits.From("a ## b").ChangesArguments);
			Assert.True(MacroTraits.From("#x").ChangesArguments);
			Assert.False(MacroTraits.From("a + b").ChangesArguments);
		}
	}
}