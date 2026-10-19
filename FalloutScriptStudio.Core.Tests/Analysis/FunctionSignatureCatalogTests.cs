namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class FunctionSignatureCatalogTests
	{
		private static FunctionSignature[] Signatures(FunctionSignatureCatalog catalog, String name)
		{
			Assert.True(catalog.TryGetSignatures(name, out FunctionSignature[] signatures), name + " has no signature");
			return signatures;
		}

		[Fact]
		public void An_opcode_line_gives_the_return_type_the_parameters_and_their_types()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(new[] { "int random (int min, int max) | - Returns a random number" }, Array.Empty<String>());

			FunctionSignature signature = Assert.Single(Signatures(catalog, "RANDOM"));

			Assert.Equal("random", signature.Name);
			Assert.Equal("int", signature.ReturnType);
			Assert.Equal(new[] { "min", "max" }, signature.Parameters.Select(p => p.Name));
			Assert.Equal(new[] { "int", "int" }, signature.Parameters.Select(p => p.Type));
			Assert.Equal(2, signature.MinArguments);
			Assert.Equal(2, signature.MaxArguments);
			Assert.True(signature.TakesParentheses);
			Assert.False(signature.IsDeprecated);
		}

		[Theory]
		[InlineData("ObjectPtr dude_obj | - the player")]
		[InlineData("ObjectPtr dude_obj() | - the player")]
		[InlineData("ObjectPtr dude_obj () | - the player")]
		[InlineData("ObjectPtr dude_obj")]
		public void A_function_without_parameters_is_written_without_parentheses(String line)
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(new[] { line }, Array.Empty<String>());

			FunctionSignature signature = Assert.Single(Signatures(catalog, "dude_obj"));

			Assert.Empty(signature.Parameters);
			Assert.False(signature.TakesParentheses);
			Assert.True(signature.Accepts(0));
			Assert.False(signature.Accepts(1));
		}

		[Fact]
		public void Optional_parameters_in_brackets_and_the_ellipsis_widen_the_range()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				new[]
				{
					"int  message_box (string message, [int flags], [int color1], [int color2]) | - box",
					"string string_format (string format, any val1, any val2, ...) | - format",
					"void group (string a, [int b, int c]) | - group of optional",
				},
				Array.Empty<String>());

			FunctionSignature box = Signatures(catalog, "message_box")[0];
			FunctionSignature format = Signatures(catalog, "string_format")[0];
			FunctionSignature group = Signatures(catalog, "group")[0];

			Assert.Equal((1, 4), (box.MinArguments, box.MaxArguments));
			Assert.Equal((3, Int32.MaxValue), (format.MinArguments, format.MaxArguments));
			Assert.Equal((1, 3), (group.MinArguments, group.MaxArguments));
			Assert.All(box.Parameters.Skip(1), p => Assert.True(p.IsOptional));
		}

		[Fact]
		public void Two_words_before_the_name_of_a_parameter_are_one_type()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(new[] { "int global_var (unsigned int var_index) | - reads" }, Array.Empty<String>());

			FunctionParameter parameter = Assert.Single(Signatures(catalog, "global_var")[0].Parameters);

			Assert.Equal("unsigned int", parameter.Type);
			Assert.Equal("var_index", parameter.Name);
		}

		[Fact]
		public void A_description_that_calls_the_function_deprecated_marks_it()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				new[] { "int old_one (int a) | - Obsolete, use new_one", "int fresh (int a) | - Returns a value" },
				Array.Empty<String>());

			Assert.True(Signatures(catalog, "old_one")[0].IsDeprecated);
			Assert.False(Signatures(catalog, "fresh")[0].IsDeprecated);
		}

		[Fact]
		public void The_handlers_comments_and_blank_lines_of_opcodes_are_ignored()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				new[] { "// VANILLA", String.Empty, "Handler start | - called", "this is not an opcode line at all !!!", "void real (int a) | - ok" },
				Array.Empty<String>());

			Assert.Equal(new[] { "real" }, catalog.Names);
		}

		[Fact]
		public void An_ini_line_gives_the_parameters_of_its_placeholders()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				Array.Empty<String>(),
				new[]
				{
					"<m>Functions",
					"<m+>Timers",
					"wait<d>[void] Pauses the game<s>wait({int time});<cr>",
					"floor<d>[int] Rounds down<s>floor({float})",
					"Tokenize<d>[string] Splits<s>Tokenize({string strMain}, {string token}, {char delimiter});",
					"get_year<d>[int] The year<s>get_year",
					"<m->",
				});

			FunctionSignature wait = Signatures(catalog, "wait")[0];
			FunctionSignature floor = Signatures(catalog, "floor")[0];
			FunctionSignature tokenize = Signatures(catalog, "tokenize")[0];
			FunctionSignature year = Signatures(catalog, "get_year")[0];

			Assert.Equal("void", wait.ReturnType);
			Assert.Equal(("int", "time"), (wait.Parameters[0].Type, wait.Parameters[0].Name));
			Assert.Equal(("float", String.Empty), (floor.Parameters[0].Type, floor.Parameters[0].Name));
			Assert.Equal(3, tokenize.Parameters.Count);
			Assert.Equal("string", tokenize.ReturnType);
			Assert.Empty(year.Parameters);
		}

		[Fact]
		public void An_ini_snippet_that_is_not_a_call_gives_no_signature_but_its_label_is_a_known_name()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				Array.Empty<String>(),
				new[]
				{
					"<m>X",
					"DEBUG<d>Constant<s>#define DEBUG (1)<cr>",
					"call [delay]<d>Call<s>call {procedure} in {delay};",
					"HOOK_TOHIT<d>ID value: 0<s>HOOK_TOHIT",
					"MyMacro<d>[int] A macro<s>MyMacro(1, 2)",
				});

			Assert.True(catalog.Contains("DEBUG"));
			Assert.False(catalog.TryGetSignatures("DEBUG", out _));
			Assert.False(catalog.TryGetSignatures("call", out _));
			Assert.False(catalog.TryGetSignatures("MyMacro", out _));
			Assert.True(catalog.Contains("mymacro"));
			Assert.True(catalog.TryGetSignatures("hook_tohit", out FunctionSignature[] constant));
			Assert.Empty(constant[0].Parameters);
		}

		[Fact]
		public void A_name_described_in_both_files_has_both_signatures()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				new[] { "void wait (int time) | - waits" },
				new[] { "<m>X", "wait<d>[void] waits<s>wait({int time}, {int more});" });

			FunctionSignature[] signatures = Signatures(catalog, "wait");

			Assert.Equal(2, signatures.Length);
			Assert.Contains(signatures, s => s.Accepts(1));
			Assert.Contains(signatures, s => s.Accepts(2));
		}

		[Fact]
		public void The_lookup_ignores_the_case_and_works_on_spans()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(new[] { "void Display_Msg (string message) | - shows" }, Array.Empty<String>());

			Assert.True(catalog.Contains("DISPLAY_MSG".AsSpan()));
			Assert.True(catalog.TryGetSignatures("display_msg".AsSpan(), out _));
			Assert.False(catalog.Contains("display".AsSpan()));
		}

		[Fact]
		public void Missing_files_give_an_empty_catalog()
		{
			using TestFolder folder = new TestFolder("signature-catalog");

			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.LoadFromFiles(folder.Combine("opcodes.txt"), folder.Combine("functions.ini"), null);

			Assert.Equal(0, catalog.Count);
			Assert.Empty(FunctionSignatureCatalog.Empty.Names);
		}

		[Fact]
		public void Files_are_read_with_their_encoding_and_a_byte_order_mark()
		{
			using TestFolder folder = new TestFolder("signature-catalog");
			String opcodes = folder.Combine("opcodes.txt");
			File.WriteAllText(opcodes, "void nice (int a) | - fine\r\nvoid second (int a, int b) | - fine\r\n", new System.Text.UTF8Encoding(true));

			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.LoadFromFiles(opcodes);

			Assert.True(catalog.TryGetSignatures("nice", out _));
			Assert.True(catalog.TryGetSignatures("second", out FunctionSignature[] second));
			Assert.Equal(2, second[0].Parameters.Count);
		}

		[Fact]
		public void The_shipped_files_describe_hundreds_of_functions_and_the_known_ones_correctly()
		{
			FunctionSignatureCatalog catalog = DiagnosticsFixture.ShippedCatalog;

			Assert.True(catalog.Count > 700, "names: " + catalog.Count);
			Assert.Equal(2, Signatures(catalog, "get_critter_stat").Max(s => s.MaxArguments));
			Assert.Equal((1, 1), (Signatures(catalog, "display_msg")[0].MinArguments, Signatures(catalog, "display_msg")[0].MaxArguments));
			Assert.False(Signatures(catalog, "dude_obj")[0].TakesParentheses);
			Assert.Equal(4, Signatures(catalog, "message_box")[0].MaxArguments);
			Assert.Equal(1, Signatures(catalog, "message_box")[0].MinArguments);
		}
	}
}