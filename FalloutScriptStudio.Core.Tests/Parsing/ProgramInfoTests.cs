namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class ProgramInfoTests
	{
		private const String ScriptFile = @"C:\scripts\test.ssl";

		private static ProgramInfo CreateInfo(MacroRegistry? globalMacros = null)
		{
			ProgramInfo parsed = new InternalParser().Parse(SslText.BasicScript(), ScriptFile, globalMacros);
			var macros = new MacroCollector(new IncludeResolver(null, null)).CollectFromText(SslText.BasicScript(), ScriptFile, false);
			return parsed.WithScriptMacros(macros);
		}

		private static ProgramInfo CreateInfoWithNativeLikeData()
		{
			var argument = new ScriptVariable("who", VariableScope.Local)
			{
				DeclaredFile = Path.GetFullPath(ScriptFile),
				DeclaredLine = 8,
				ArgumentDeclaredLine = 8,
			};
			var local = new ScriptVariable("tmp", VariableScope.Local) { DeclaredFile = Path.GetFullPath(ScriptFile), DeclaredLine = 9 };
			var global = new ScriptVariable("counter", VariableScope.Global)
			{
				DeclaredFile = Path.GetFullPath(ScriptFile),
				DeclaredLine = 4,
				InitialValue = "5",
				InitialValueKind = ValueKind.Int,
			};
			var start = new ScriptProcedure("start")
			{
				DeclaredFile = Path.GetFullPath(ScriptFile),
				StartFile = Path.GetFullPath(ScriptFile),
				DeclaredLine = 5,
				DefinedLine = 8,
				StartLine = 8,
				EndLine = 10,
				ArgumentCount = 1,
				Variables = new[] { argument, local },
				References = new[] { new SourceReference(Path.GetFullPath(ScriptFile), 9) },
			};
			return new ProgramInfo(new[] { start }, new[] { global }, null, null, ScriptFile).WithFlags(true, true, false, false);
		}

		[Fact]
		public void Lookup_FindsProceduresCaseInsensitively()
		{
			ProgramInfo info = CreateInfo();

			IScriptSymbol? symbol = info.Lookup("TALK_P_PROC");

			Assert.NotNull(symbol);
			Assert.Equal(SymbolKind.Procedure, symbol.Kind);
			Assert.Equal("Talk_p_proc", symbol.Name);
			Assert.Equal(SymbolKind.None, info.LookupKind("missing", null, 0));
		}

		[Fact]
		public void Lookup_MacrosAreCaseSensitiveAndFallBackToGlobalMacros()
		{
			var registry = new MacroRegistry();
			registry.Replace(new[] { new ScriptMacro("GLOBAL_MACRO", "GLOBAL_MACRO", "1", @"C:\h\g.h", 1, null) });
			ProgramInfo info = CreateInfo(registry).WithGlobalMacros(registry);

			Assert.Equal(SymbolKind.Macro, info.LookupKind("NAME", null, 0));
			Assert.Equal(SymbolKind.None, info.LookupKind("name", null, 0));
			Assert.Equal(SymbolKind.Macro, info.LookupKind("GLOBAL_MACRO", null, 0));
			Assert.Equal("SCRIPT_TEST", info.FindMacro("NAME")?.Code);
		}

		[Fact]
		public void Lookup_FindsLocalVariablesInsideTheirProcedureOnly()
		{
			ProgramInfo info = CreateInfoWithNativeLikeData();
			String file = Path.GetFullPath(ScriptFile);

			IScriptSymbol? inside = info.Lookup("TMP", file, 9);
			IScriptSymbol? outside = info.Lookup("tmp", file, 20);

			Assert.Equal(SymbolKind.LocalVariable, inside?.Kind);
			Assert.Null(outside);
			Assert.Equal(SymbolKind.GlobalVariable, info.LookupKind("counter", file, 20));
			Assert.Equal("Argument variable who", info.LookupDescription("who", file, 8));
			Assert.Equal("Variable counter := 5", info.LookupDescription("counter", null, 0));
		}

		[Fact]
		public void LookupDeclarationDefinitionAndReferences()
		{
			ProgramInfo info = CreateInfoWithNativeLikeData();
			String file = Path.GetFullPath(ScriptFile);

			Assert.Equal(new SourceReference(file, 5), info.LookupDeclaration("start", null, 0));
			Assert.Equal(new SourceReference(file, 8), info.LookupDefinition("START"));
			Assert.Null(info.LookupDefinition("missing"));
			Assert.Equal(5, info.GetProcedureDeclarationLine("start"));
			Assert.Equal(-1, info.GetProcedureDeclarationLine("missing"));
			Assert.Equal(9, info.LookupReferences("start", null, 0)?.Single().Line);
			Assert.Null(info.LookupReferences("missing", null, 0));
		}

		[Fact]
		public void ProcedureDescription_ListsModifiersAndArguments()
		{
			ProgramInfo info = CreateInfoWithNativeLikeData();
			ScriptProcedure start = info.Procedures[0];

			Assert.Equal("procedure start (who)", start.FullDescription);
			Assert.Equal("start (who)", start.ShortDescription);

			var exported = new ScriptProcedure("api") { Flags = ProcedureFlags.Export | ProcedureFlags.Critical, ArgumentCount = 2 };
			Assert.Equal("exported critical procedure api (x, x)", exported.FullDescription);
		}

		[Fact]
		public void GetProcedureFromPosition_UsesOneBasedLines()
		{
			ProgramInfo info = CreateInfo();

			Assert.Equal("start", info.GetProcedureFromPosition(9)?.Name);
			Assert.Equal("Talk_p_proc", info.GetProcedureFromPosition(12)?.Name);
			Assert.Null(info.GetProcedureFromPosition(11));
			Assert.Null(info.GetProcedureFromPosition(1));
		}

		[Fact]
		public void GetNearProcedure_ReturnsNextProcedureOrTheLastOne()
		{
			ProgramInfo info = CreateInfo();

			Assert.Equal("start", info.GetNearProcedure(2)?.Name);
			Assert.Equal("Talk_p_proc", info.GetNearProcedure(11)?.Name);
			Assert.Equal("Talk_p_proc", info.GetNearProcedure(100)?.Name);
			Assert.Null(new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>()).GetNearProcedure(1));
		}

		[Fact]
		public void GetTopProcedure_IgnoresDeclarationsWithoutBody()
		{
			var declared = new ScriptProcedure("declared") { DeclaredLine = 1 };
			var later = new ScriptProcedure("later") { StartLine = 20, EndLine = 22 };
			var earlier = new ScriptProcedure("earlier") { StartLine = 10, EndLine = 12 };
			var info = new ProgramInfo(new[] { declared, later, earlier }, Array.Empty<ScriptVariable>());

			Assert.Equal("earlier", info.GetTopProcedure()?.Name);
		}

		[Fact]
		public void ProcedureLookups_ByName()
		{
			ProgramInfo info = CreateInfo();

			Assert.True(info.ProcedureExists("START"));
			Assert.False(info.ProcedureExists("nope"));
			Assert.Equal(1, info.GetProcedureIndex("talk_p_proc"));
			Assert.Equal("start", info.GetProcedure("Start")?.Name);
			Assert.Null(info.GetProcedure("nope"));
		}

		[Fact]
		public void NameExists_WithoutNativeData_ChecksProceduresAndScriptMacros()
		{
			ProgramInfo info = CreateInfo();

			Assert.True(info.NameExists("start", SymbolKind.Procedure));
			Assert.True(info.NameExists("NAME", SymbolKind.Macro));
			Assert.False(info.NameExists("name", SymbolKind.Macro));
			Assert.False(info.NameExists("unused", SymbolKind.GlobalVariable));
		}

		[Fact]
		public void NameExists_WithNativeData_LocalVariablesOnlyConflictWithLocalVariables()
		{
			ProgramInfo info = CreateInfoWithNativeLikeData();
			String file = Path.GetFullPath(ScriptFile);

			Assert.True(info.NameExists("tmp", SymbolKind.LocalVariable, file, 9));
			Assert.False(info.NameExists("tmp", SymbolKind.Procedure, file, 9));
			Assert.True(info.NameExists("counter", SymbolKind.Procedure));
			Assert.False(info.NameExists("counter", SymbolKind.LocalVariable));
		}

		[Fact]
		public void ContainsName_CanIgnoreDeclarationsWithoutBody()
		{
			var declared = new ScriptProcedure("declared") { DeclaredLine = 1 };
			var info = new ProgramInfo(new[] { declared }, Array.Empty<ScriptVariable>());

			Assert.True(info.ContainsName("declared", true, false));
			Assert.False(info.ContainsName("declared", false, false));
		}

		[Fact]
		public void ScriptNameId_FollowsMacroAliases()
		{
			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal)
			{
				["NAME"] = new ScriptMacro("NAME", "NAME", "SCRIPT_TEST", "a.ssl", 1, null),
				["SCRIPT_TEST"] = new ScriptMacro("SCRIPT_TEST", "SCRIPT_TEST", "(12)", "define.h", 1, null),
			};
			var info = new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>(), macros);

			Assert.Equal(12, info.ScriptNameId);
			Assert.Equal(-1, new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>()).ScriptNameId);
		}

		[Fact]
		public void ScriptNameId_CircularAliases_ReturnMinusOne()
		{
			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal)
			{
				["NAME"] = new ScriptMacro("NAME", "NAME", "OTHER", "a.ssl", 1, null),
				["OTHER"] = new ScriptMacro("OTHER", "OTHER", "NAME", "a.ssl", 2, null),
			};
			var info = new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>(), macros);

			Assert.Equal(-1, info.ScriptNameId);
		}

		[Theory]
		[InlineData("1", true)]
		[InlineData("(1)", true)]
		[InlineData("true", true)]
		[InlineData("0", false)]
		[InlineData("False", false)]
		[InlineData("whatever", false)]
		public void ShortCircuitEvaluation_ReadsTheMacroValue(String code, Boolean expected)
		{
			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal)
			{
				["SFALL_SC_EVALUATION"] = new ScriptMacro("SFALL_SC_EVALUATION", "SFALL_SC_EVALUATION", code, "a.ssl", 1, null),
			};
			var info = new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>(), macros);

			Assert.Equal(expected, info.ShortCircuitEvaluation);
			Assert.False(new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>()).ShortCircuitEvaluation);
		}

		[Fact]
		public void GetAutosuggest_ListsOpcodesProceduresVariablesAndMacrosWithoutDuplicates()
		{
			var catalog = new OpcodeCatalog(new[] { "void talk_to (ObjectPtr who) | - Starts a talk.", "int tal_count | - Counts." });
			var registry = new MacroRegistry();
			registry.Replace(new[]
			{
				new ScriptMacro("TALK_MACRO", "TALK_MACRO(x)", "x", @"C:\h\g.h", 1, null),
				new ScriptMacro("TALK_HUGE", "TALK_HUGE", new String('x', 400), @"C:\h\g.h", 2, null),
			});
			ProgramInfo info = CreateInfo(registry).WithGlobalMacros(registry);
			info = info.WithProcedures(info.Procedures.Concat(new[] { new ScriptProcedure("talk_helper") { StartLine = 30 } }));

			IReadOnlyList<AutosuggestItem> items = info.GetAutosuggest("tal", catalog);

			Assert.Equal(
				new[] { "tal_count", "talk_to", "Talk_p_proc", "talk_helper", "TALK_HUGE", "TALK_MACRO(x)" },
				items.Select(i => i.Name));
			Assert.Equal(AutosuggestKind.Opcode, items[0].Kind);
			Assert.Equal(AutosuggestKind.Procedure, items[2].Kind);
			Assert.Equal("Define:\nNo preview macros.", items[4].Description);
			Assert.Equal("Define:\nx", items[5].Description);
		}

		[Fact]
		public void GetAutosuggest_RemovesDuplicatedNames()
		{
			var catalog = new OpcodeCatalog(new[] { "void start (int x) | - Opcode named like a procedure." });
			ProgramInfo info = CreateInfo();

			IReadOnlyList<AutosuggestItem> items = info.GetAutosuggest("start", catalog);

			AutosuggestItem item = Assert.Single(items);
			Assert.Equal(AutosuggestKind.Opcode, item.Kind);
		}

		[Fact]
		public void WithMethods_DoNotModifyTheOriginal()
		{
			ProgramInfo info = CreateInfo();

			ProgramInfo changed = info.WithFlags(hasParseError: true, needsDataRefresh: true);

			Assert.False(info.HasParseError);
			Assert.True(changed.HasParseError);
			Assert.True(changed.NeedsDataRefresh);
			Assert.True(changed.IsParsed);
			Assert.Same(info.Procedures, changed.Procedures);
		}
	}
}