namespace FalloutScriptStudio.Core.Tests.Globals
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class GlobalVariableIndexTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("global-variables");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		private static GlobalVariableIndex Build(params (String Path, String Text)[] files)
		{
			return GlobalVariableIndex.FromTexts(files.Select(static f => new KeyValuePair<String, String>(f.Path, f.Text)));
		}

		private static GlobalVariable Single(GlobalVariableIndex index, GlobalVariableKind kind, String key)
		{
			return index.Variables.Single(v => v.Kind == kind && String.Equals(v.Key, key, StringComparison.Ordinal));
		}

		private String Write(String relativePath, String content)
		{
			String path = Path.GetFullPath(this._folder.Write(relativePath, content));
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-3));
			return path;
		}

		private static ProjectIndexRoots Roots(String folder)
		{
			return new ProjectIndexRoots { Folders = new[] { new ProjectIndexRoot(folder, true) } };
		}

		[Fact]
		public void A_macro_and_its_number_are_the_same_variable()
		{
			GlobalVariableIndex index = Build(
				("a:\\h.h", "#define GVAR_FOO (27)\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(GVAR_FOO, 1);\r\n\tvariable v := global_var(27);\r\nend\r\n"));

			GlobalVariable variable = Single(index, GlobalVariableKind.GameVariable, "27");
			Assert.Equal(27L, variable.Id);
			Assert.Equal(GlobalKeyKind.Id, variable.KeyKind);
			Assert.Equal(new[] { "GVAR_FOO" }, variable.Aliases);
			Assert.Equal("GVAR_FOO (27)", variable.DisplayName);
			Assert.Equal(2, variable.Uses.Count);
			Assert.Equal(1, variable.ReadCount);
			Assert.Equal(1, variable.WriteCount);
		}

		[Fact]
		public void A_macro_of_an_unknown_header_stays_symbolic()
		{
			GlobalVariableIndex index = Build(("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(GVAR_UNKNOWN, 1);\r\nend\r\n"));

			GlobalVariable variable = Single(index, GlobalVariableKind.GameVariable, "GVAR_UNKNOWN");
			Assert.Equal(GlobalKeyKind.Symbol, variable.KeyKind);
			Assert.Null(variable.Id);
			Assert.Equal("GVAR_UNKNOWN", variable.DisplayName);
		}

		[Fact]
		public void A_macro_that_names_another_macro_is_followed()
		{
			GlobalVariableIndex index = Build(
				("a:\\h.h", "#define GVAR_BASE (5)\r\n#define GVAR_ALIAS GVAR_BASE\r\n#define GVAR_ALIAS2 (GVAR_ALIAS)\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(GVAR_ALIAS2, 1);\r\nend\r\n"));

			Assert.Equal(5L, Single(index, GlobalVariableKind.GameVariable, "5").Id);
		}

		[Fact]
		public void A_loop_of_macros_does_not_hang_and_stays_symbolic()
		{
			GlobalVariableIndex index = Build(
				("a:\\h.h", "#define GVAR_A GVAR_B\r\n#define GVAR_B GVAR_A\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(GVAR_A, 1);\r\nend\r\n"));

			Assert.Equal(GlobalKeyKind.Symbol, index.Variables.Single().KeyKind);
		}

		[Fact]
		public void A_macro_defined_twice_with_different_values_is_resolved_only_in_its_own_file()
		{
			GlobalVariableIndex index = Build(
				("a:\\a.ssl", "#define LVAR_Flags (0)\r\nprocedure start begin\r\n\tset_local_var(LVAR_Flags, 1);\r\nend\r\n"),
				("a:\\b.ssl", "#define LVAR_Flags (3)\r\nprocedure start begin\r\n\tset_local_var(LVAR_Flags, 1);\r\nend\r\n"),
				("a:\\c.ssl", "procedure start begin\r\n\tset_global_var(LVAR_Flags, 1);\r\nend\r\n"));

			Assert.Equal(new[] { "0", "3" }, index.Variables.Where(static v => v.Kind == GlobalVariableKind.LocalVariable).Select(static v => v.Key).OrderBy(static k => k, StringComparer.Ordinal).ToArray());
			Assert.Equal(GlobalKeyKind.Symbol, index.Variables.Single(static v => v.Kind == GlobalVariableKind.GameVariable).KeyKind);
		}

		[Fact]
		public void Local_variables_of_different_scripts_are_different_variables()
		{
			GlobalVariableIndex index = Build(
				("a:\\a.ssl", "procedure start begin\r\n\tset_local_var(0, 1);\r\nend\r\n"),
				("a:\\b.ssl", "procedure start begin\r\n\tvariable v := local_var(0);\r\nend\r\n"));

			Assert.Equal(2, index.Variables.Count(static v => v.Kind == GlobalVariableKind.LocalVariable));
			Assert.All(index.Variables, static v => Assert.NotNull(v.Scope));
		}

		[Fact]
		public void Map_variables_with_a_macro_are_known_by_the_macro_because_the_numbers_of_the_maps_overlap()
		{
			GlobalVariableIndex index = Build(
				("a:\\map1.h", "#define MVAR_Town_Door (2)\r\n"),
				("a:\\map2.h", "#define MVAR_Cave_Door (2)\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_map_var(MVAR_Town_Door, 1);\r\n\tset_map_var(MVAR_Cave_Door, 1);\r\n\tset_map_var(MVAR_Cave_Door, 0);\r\nend\r\n"));

			Assert.Equal(2, index.Variables.Count);
			Assert.Equal(new[] { "MVAR_Cave_Door (2)", "MVAR_Town_Door (2)" }, index.Variables.Select(static v => v.DisplayName).ToArray());
		}

		[Fact]
		public void Sfall_globals_by_name_and_by_number_are_kept_apart_and_a_macro_may_give_either()
		{
			GlobalVariableIndex index = Build(
				("a:\\h.h", "#define SG_NAME \"abcdefgh\"\r\n#define SG_ID (1234)\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_sfall_global(SG_NAME, 1);\r\n\tvariable a := get_sfall_global_int(\"abcdefgh\");\r\n\tset_sfall_global(SG_ID, 2);\r\n\ta := get_sfall_global_int(1234);\r\nend\r\n"));

			GlobalVariable named = Single(index, GlobalVariableKind.SfallGlobal, "abcdefgh");
			Assert.Equal(GlobalKeyKind.Name, named.KeyKind);
			Assert.Equal(2, named.Uses.Count);
			Assert.Equal(new[] { "SG_NAME" }, named.Aliases);
			GlobalVariable numbered = Single(index, GlobalVariableKind.SfallGlobal, "1234");
			Assert.Equal(GlobalKeyKind.Id, numbered.KeyKind);
			Assert.Equal(2, numbered.Uses.Count);
		}

		[Fact]
		public void The_sfall_names_are_case_sensitive()
		{
			GlobalVariableIndex index = Build(("a:\\a.ssl", "procedure start begin\r\n\tset_sfall_global(\"AbCdEfGh\", 1);\r\n\tset_sfall_global(\"abcdefgh\", 1);\r\nend\r\n"));

			Assert.Equal(2, index.Variables.Count);
		}

		[Fact]
		public void The_type_of_a_value_comes_from_the_function_or_the_literal()
		{
			GlobalVariableIndex index = Build(
				("a:\\h.h", "#define HALF (0.5)\r\n#define ONE (1)\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_sfall_global(\"abcdefgh\", 1);\r\n\tset_sfall_global(\"abcdefgh\", 1.5);\r\n\tset_sfall_global(\"abcdefgh\", HALF);\r\n\tset_sfall_global(\"abcdefgh\", ONE);\r\n\tset_sfall_global(\"abcdefgh\", 2 * 3);\r\n\tvariable a := get_sfall_global_float(\"abcdefgh\");\r\n\ta := get_sfall_global_int(\"abcdefgh\");\r\nend\r\n"));

			GlobalVariableUse[] uses = index.Variables.Single().Uses.OrderBy(static u => u.Line).ToArray();
			Assert.Equal(
				new[] { GlobalValueType.Int, GlobalValueType.Float, GlobalValueType.Float, GlobalValueType.Int, GlobalValueType.Unknown, GlobalValueType.Float, GlobalValueType.Int },
				uses.Select(static u => u.ValueType).ToArray());
			Assert.Equal(new[] { "1", "1.5", "0.5", "1", null, null, null }, uses.Select(static u => u.Literal).ToArray());
		}

		[Fact]
		public void The_literal_of_a_game_variable_is_kept()
		{
			GlobalVariableIndex index = Build(("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(3, -4);\r\n\tset_global_var(3, 0x10);\r\nend\r\n"));

			Assert.Equal(new[] { "-4", "0x10" }, index.Variables.Single().Uses.Select(static u => u.Literal).ToArray());
			Assert.All(index.Variables.Single().Uses, static u => Assert.Equal(GlobalValueType.Unknown, u.ValueType));
		}

		[Fact]
		public void Uses_are_ordered_by_file_and_line_and_remember_their_procedure()
		{
			GlobalVariableIndex index = Build(
				("a:\\b.ssl", "procedure two begin\r\n\tset_global_var(1, 1);\r\nend\r\n"),
				("a:\\a.ssl", "procedure one begin\r\n\tset_global_var(1, 1);\r\n\tvariable v := global_var(1);\r\nend\r\n"));

			GlobalVariableUse[] uses = index.Variables.Single().Uses.ToArray();
			Assert.Equal(new[] { "a.ssl", "a.ssl", "b.ssl" }, uses.Select(static u => u.FileName).ToArray());
			Assert.Equal(new[] { 2, 3, 2 }, uses.Select(static u => u.Line).ToArray());
			Assert.Equal(new[] { "one", "one", "two" }, uses.Select(static u => u.Procedure).ToArray());
		}

		[Fact]
		public void Variables_are_ordered_by_family_then_by_name_with_the_numbers_last()
		{
			GlobalVariableIndex index = Build(("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(10, 1);\r\n\tset_global_var(9, 1);\r\n\tset_global_var(GVAR_B, 1);\r\n\tset_global_var(GVAR_A, 1);\r\n\tset_sfall_global(\"zzzzzzzz\", 1);\r\n\tset_map_var(1, 1);\r\nend\r\n"));

			Assert.Equal(
				new[] { "zzzzzzzz", "GVAR_A", "GVAR_B", "9", "10", "1" },
				index.Variables.Select(static v => v.DisplayName).ToArray());
		}

		[Fact]
		public void Variables_are_found_by_any_of_their_names()
		{
			GlobalVariableIndex index = Build(
				("a:\\h.h", "#define GVAR_FOO (27)\r\n"),
				("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(GVAR_FOO, 1);\r\n\tset_sfall_global(\"abcdefgh\", 1);\r\nend\r\n"));

			Assert.Single(index.FindByName("gvar_foo"));
			Assert.Single(index.FindByName("ABCDEFGH"));
			Assert.Empty(index.FindByName("nothing"));
		}

		[Fact]
		public void An_empty_project_gives_the_empty_index()
		{
			Assert.Same(GlobalVariableIndex.Empty, Build());
			Assert.Empty(GlobalVariableIndex.Empty.Variables);
			Assert.Equal(0, GlobalVariableIndex.Empty.UseCount);
		}

		[Fact]
		public void Counts_cover_the_uses_and_the_files()
		{
			GlobalVariableIndex index = Build(
				("a:\\a.ssl", "procedure start begin\r\n\tset_global_var(1, 1);\r\n\tset_global_var(2, 1);\r\nend\r\n"),
				("a:\\b.ssl", "procedure start begin\r\n\tset_global_var(1, 1);\r\nend\r\n"),
				("a:\\c.ssl", "procedure start begin\r\nend\r\n"));

			Assert.Equal(3, index.UseCount);
			Assert.Equal(2, index.FileCount);
			Assert.Equal(2, index.Variables.Single(static v => v.Key == "1").FileCount);
		}

		[Fact]
		public async Task The_project_index_builds_the_variables_and_follows_a_saved_file()
		{
			this.Write("h.h", "#define GVAR_FOO (27)\r\n");
			String script = this.Write("a.ssl", "#include \"h.h\"\r\nprocedure start begin\r\n\tset_global_var(GVAR_FOO, 1);\r\nend\r\n");
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(this._folder.Path));

			GlobalVariableIndex first = index.GetGlobalVariables();
			Assert.Equal("GVAR_FOO (27)", first.Variables.Single().DisplayName);
			Assert.Same(first, index.GetGlobalVariables());

			await index.UpdateFileAsync(script, "procedure start begin\r\n\tset_global_var(27, 1);\r\n\tvariable v := global_var(27);\r\nend\r\n");

			GlobalVariableIndex second = index.GetGlobalVariables();
			Assert.NotSame(first, second);
			Assert.Equal(2, second.Variables.Single().Uses.Count);
			Assert.Equal("27", second.Variables.Single().DisplayName);
		}

		[Fact]
		public async Task A_removed_file_leaves_the_index()
		{
			String script = this.Write("a.ssl", "procedure start begin\r\n\tset_global_var(1, 1);\r\nend\r\n");
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(this._folder.Path));
			Assert.Single(index.GetGlobalVariables().Variables);

			File.Delete(script);
			await index.UpdateFileAsync(script);

			Assert.Empty(index.GetGlobalVariables().Variables);
		}

		[Fact]
		public async Task An_unchanged_run_keeps_the_same_variables()
		{
			this.Write("a.ssl", "procedure start begin\r\n\tset_global_var(1, 1);\r\nend\r\n");
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(this._folder.Path));
			GlobalVariableIndex first = index.GetGlobalVariables();

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Same(first, index.GetGlobalVariables());
		}

		[Fact]
		public async Task The_limit_of_uses_leaves_files_out_and_says_so()
		{
			for (Int32 i = 0; i < 20; i++)
			{
				this.Write("f" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".ssl", "procedure start begin\r\n\tset_global_var(1, 1);\r\n\tset_global_var(2, 1);\r\nend\r\n");
			}

			using ProjectSymbolIndex index = new ProjectSymbolIndex(() => new ProjectIndexOptions { MaxGlobalUses = 10, MaxDegreeOfParallelism = 1 });
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.True(index.AreGlobalsTruncated);
			GlobalVariableIndex globals = index.GetGlobalVariables();
			Assert.True(globals.IsTruncated);
			Assert.Equal(10, globals.UseCount);
			Assert.Equal(20, index.FileCount);
		}

		[Fact]
		public async Task Header_macros_resolve_the_uses_of_a_script_indexed_before_them()
		{
			this.Write("a.ssl", "procedure start begin\r\n\tset_global_var(GVAR_ZED, 1);\r\nend\r\n");
			this.Write("z.h", "#define GVAR_ZED (5)\r\n");
			using ProjectSymbolIndex index = new ProjectSymbolIndex(() => new ProjectIndexOptions { MaxDegreeOfParallelism = 1 });
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal("GVAR_ZED (5)", index.GetGlobalVariables().Variables.Single().DisplayName);
		}

		[Fact]
		public void Clearing_the_index_forgets_the_variables()
		{
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			index.Clear();

			Assert.Empty(index.GetGlobalVariables().Variables);
		}
	}
}