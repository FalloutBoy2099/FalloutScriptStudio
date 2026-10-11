namespace FalloutScriptStudio.Core.Tests.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.Tests.TextTools;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class RefactoringServiceTests
	{
		private const String Script = @"C:\mod\scripts\test.ssl";
		private const String Header = @"C:\mod\headers\test.h";

		private readonly RefactoringService _service = new RefactoringService();

		private static String ScriptText()
		{
			return TestScripts.Crlf(
				"// Test",                                  // 1
				"#include \"test.h\"",                      // 2
				"#define MSG_HELLO  100",                   // 3
				"#define NOptionX(a)   (a)",                // 4
				"",                                         // 5
				"variable counter := 0;",                   // 6
				"",                                         // 7
				"procedure start;",                         // 8
				"procedure Node001;",                       // 9
				"",                                         // 10
				"procedure start begin",                    // 11
				"   variable tmp := 0;",                    // 12
				"   counter := counter + 1; tmp := tmp;",  // 13
				"   call Node001;",                         // 14
				"end",                                      // 15
				"",                                         // 16
				"procedure Node001 begin",                  // 17
				"   Reply(MSG_HELLO);",                     // 18
				"   counter := 2 + counter;",               // 19
				"end");                                     // 20
		}

		private static SourceReference Ref(Int32 line)
		{
			return new SourceReference(Script, line);
		}

		private static ProgramInfo CreateInfo(Boolean withReferences = true)
		{
			var tmp = new ScriptVariable("tmp", VariableScope.Local)
			{
				DeclaredFile = Script,
				DeclaredLine = 12,
				References = withReferences ? new[] { Ref(13), Ref(13) } : Array.Empty<SourceReference>(),
			};
			var counter = new ScriptVariable("counter", VariableScope.Global)
			{
				DeclaredFile = Script,
				DeclaredLine = 6,
				References = withReferences ? new[] { Ref(13), Ref(13), Ref(19), Ref(19) } : Array.Empty<SourceReference>(),
			};
			var start = new ScriptProcedure("start")
			{
				DeclaredFile = Script, StartFile = Script, DeclaredLine = 8, DefinedLine = 11, StartLine = 11, EndLine = 15, Variables = new[] { tmp },
			};
			var node = new ScriptProcedure("Node001")
			{
				DeclaredFile = Script, StartFile = Script, DeclaredLine = 9, DefinedLine = 17, StartLine = 17, EndLine = 20,
				References = withReferences ? new[] { Ref(14) } : Array.Empty<SourceReference>(),
			};
			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal)
			{
				["MSG_HELLO"] = new ScriptMacro("MSG_HELLO", "MSG_HELLO", "100", Script, 3, null),
				["NOptionX"] = new ScriptMacro("NOptionX", "NOptionX(a)", "(a)", Script, 4, null),
			};
			return new ProgramInfo(new[] { start, node }, new[] { counter }, macros, null, Script).WithFlags(isParsed: true, hasNativeData: true);
		}

		private static RenameRequest Request(ProgramInfo info, IScriptSymbol symbol, String newName, params RefactorDocument[] documents)
		{
			return new RenameRequest(info, symbol, newName, Script, documents.Length == 0 ? new[] { new RefactorDocument(Script, ScriptText(), true) } : documents);
		}

		private static String Apply(RenameResult result, String path, String text)
		{
			FileEdits? file = result.Files.FirstOrDefault(f => String.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
			return file is null ? text : file.ApplyTo(text);
		}

		[Fact]
		public void Validate_RejectsBadNames()
		{
			ProgramInfo info = CreateInfo();
			IScriptSymbol counter = info.Lookup("counter")!;

			Assert.Equal(RenameError.EmptyName, this._service.Validate(Request(info, counter, "  ")).Error);
			Assert.Equal(RenameError.InvalidName, this._service.Validate(Request(info, counter, "bad name")).Error);
			Assert.Equal(RenameError.InvalidName, this._service.Validate(Request(info, counter, "1abc")).Error);
			Assert.Equal(RenameError.InvalidName, this._service.Validate(Request(info, counter, "Begin")).Error);
			Assert.Equal(RenameError.SameName, this._service.Validate(Request(info, counter, "counter")).Error);
			Assert.Equal(RenameError.NameExists, this._service.Validate(Request(info, counter, "Node001")).Error);
			Assert.Equal(RenameError.NameExists, this._service.Validate(Request(info, counter, "START")).Error);
			Assert.Equal(RenameError.NameExists, this._service.Validate(Request(info, counter, "MSG_HELLO")).Error);
		}

		[Fact]
		public void Validate_AcceptsGoodNamesAndCaseOnlyChanges()
		{
			ProgramInfo info = CreateInfo();

			RenameValidation fresh = this._service.Validate(Request(info, info.Lookup("counter")!, " total "));
			RenameValidation caseOnly = this._service.Validate(Request(info, info.Lookup("counter")!, "Counter"));

			Assert.True(fresh.IsValid);
			Assert.Equal("total", fresh.NewName);
			Assert.True(caseOnly.IsValid);
		}

		[Fact]
		public void Validate_LocalVariableOnlyConflictsWithLocals()
		{
			ProgramInfo info = CreateInfo();
			IScriptSymbol tmp = info.Lookup("tmp", Script, 12)!;

			Assert.Equal(SymbolKind.LocalVariable, tmp.Kind);
			Assert.True(this._service.Validate(Request(info, tmp, "counter")).IsValid);
			Assert.True(this._service.Validate(Request(info, tmp, "Node001")).IsValid);
			Assert.True(this._service.Validate(Request(info, tmp, "other")).IsValid);
		}

		[Fact]
		public void Rename_GlobalVariable_RenamesDeclarationAndEveryReference()
		{
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.Lookup("counter")!, "total"));

			Assert.True(result.Succeeded);
			String renamed = Apply(result, Script, ScriptText());
			Assert.Contains("variable total := 0;", renamed);
			Assert.Contains("   total := total + 1; tmp := tmp;", renamed);
			Assert.Contains("   total := 2 + total;", renamed);
			Assert.DoesNotContain("counter", renamed);
			Assert.Equal(5, result.EditCount);
			Assert.Empty(result.UnresolvedReferences);
			Assert.True(result.Files.Single().IsOpen);
		}

		[Fact]
		public void Rename_Variable_OnlyTouchesLinesThatAreReferences()
		{
			// The word also appears in a comment; without a reference on that line it is left alone.
			String text = ScriptText().Replace("// Test", "// counter is a counter");
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.Lookup("counter")!, "total", new RefactorDocument(Script, text)));

			Assert.StartsWith("// counter is a counter", Apply(result, Script, text));
		}

		[Fact]
		public void Rename_LocalVariable_UsesItsOwnReferences()
		{
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.Lookup("tmp", Script, 12)!, "temp"));

			String renamed = Apply(result, Script, ScriptText());
			Assert.Contains("   variable temp := 0;", renamed);
			Assert.Contains("counter := counter + 1; temp := temp;", renamed);
		}

		[Fact]
		public void Rename_Variable_ReferencesInUnsuppliedFiles_AreReportedAsUnresolved()
		{
			var variable = new ScriptVariable("counter", VariableScope.Global)
			{
				DeclaredFile = Script,
				DeclaredLine = 6,
				References = new[] { Ref(13), new SourceReference(Header, 3) },
			};
			var info = new ProgramInfo(Array.Empty<ScriptProcedure>(), new[] { variable }, null, null, Script).WithFlags(hasNativeData: true);

			RenameResult result = this._service.Rename(Request(info, variable, "total"));

			Assert.Equal(new[] { new SourceReference(Header, 3) }, result.UnresolvedReferences.ToArray());
		}

		[Fact]
		public void Rename_Variable_ReferencesAreMatchedPerFile()
		{
			String header = TestScripts.Crlf("// h", "x := counter;", "counter := 1;", "y := 2;");
			var counter = new ScriptVariable("counter", VariableScope.Global)
			{
				DeclaredFile = Script,
				DeclaredLine = 6,
				References = new[] { Ref(13), new SourceReference(Header, 3) },
			};
			ProgramInfo info = new ProgramInfo(Array.Empty<ScriptProcedure>(), new[] { counter }, null, null, Script).WithFlags(hasNativeData: true);

			RenameResult result = this._service.Rename(Request(info, counter, "total", new RefactorDocument(Script, ScriptText(), true), new RefactorDocument(Header, header)));

			// Only line 3 of the header is a reference; line 2 also contains the word but is not.
			Assert.Equal(TestScripts.Crlf("// h", "x := counter;", "total := 1;", "y := 2;"), Apply(result, Header, header));
			Assert.Contains("variable total := 0;", Apply(result, Script, ScriptText()));
		}

		[Fact]
		public void Rename_ProcedureWithReferences_RenamesDeclarationDefinitionAndCalls()
		{
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.GetProcedure("node001")!, "NodeA"));

			String renamed = Apply(result, Script, ScriptText());
			Assert.Equal(ScriptText().Replace("Node001", "NodeA"), renamed);
			Assert.Equal(3, result.EditCount);
		}

		[Fact]
		public void Rename_ProcedureWithoutReferences_UsesThePatternOfDeclarationsAndCalls()
		{
			ProgramInfo info = CreateInfo(withReferences: false);
			String text = ScriptText().Replace("   call Node001;", "   call  Node001;\r\n   NOption(1, Node001, 4);\r\n   x := Node001x;\r\n   y := (Node001);");

			RenameResult result = this._service.Rename(Request(info, info.GetProcedure("node001")!, "NodeA", new RefactorDocument(Script, text)));

			String renamed = Apply(result, Script, text);
			Assert.Contains("procedure NodeA;", renamed);
			Assert.Contains("procedure NodeA begin", renamed);
			Assert.Contains("   call NodeA;", renamed);
			Assert.Contains("NOption(1, NodeA, 4);", renamed);
			Assert.Contains("y := (NodeA);", renamed);
			Assert.Contains("x := Node001x;", renamed);
		}

		[Fact]
		public void Rename_ProcedureDefinedInAnotherFile_CanRenameThereToo()
		{
			String include = TestScripts.Crlf("procedure Shared;", "procedure Shared begin", "   x := Shared;", "end");
			var shared = new ScriptProcedure("Shared")
			{
				DeclaredFile = Header, StartFile = Header, DeclaredLine = 1, DefinedLine = 2, StartLine = 2, EndLine = 4,
			};
			ProgramInfo info = new ProgramInfo(new[] { shared }, Array.Empty<ScriptVariable>(), null, null, Script).WithFlags(hasNativeData: true);
			String script = TestScripts.Crlf("#include \"test.h\"", "procedure start begin", "   call Shared;", "end");
			var documents = new[] { new RefactorDocument(Script, script, true), new RefactorDocument(Header, include) };

			RenameResult onlyCurrent = this._service.Rename(Request(info, shared, "Common", documents));
			RenameResult both = this._service.Rename(Request(info, shared, "Common", documents) with { RenameInDefiningFile = true });

			Assert.Equal(Script, Assert.Single(onlyCurrent.Files).Path);
			Assert.Equal(2, both.Files.Count);
			Assert.Equal(TestScripts.Crlf("procedure Common;", "procedure Common begin", "   x := Common;", "end"), Apply(both, Header, include));
			Assert.Empty(both.Warnings);
		}

		[Fact]
		public void Rename_ProcedureDefinedElsewhere_WarnsWhenTheFileIsMissing()
		{
			var shared = new ScriptProcedure("Shared") { DeclaredFile = Header, StartFile = Header, DeclaredLine = 1, DefinedLine = 2, StartLine = 2, EndLine = 4 };
			ProgramInfo info = new ProgramInfo(new[] { shared }, Array.Empty<ScriptVariable>(), null, null, Script).WithFlags(hasNativeData: true);

			RenameResult result = this._service.Rename(Request(info, shared, "Common") with { RenameInDefiningFile = true });

			Assert.Single(result.Warnings);
		}

		[Fact]
		public void Rename_LocalMacro_RenamesWholeWordsCaseSensitive_AndRealignsTheDefine()
		{
			ProgramInfo info = CreateInfo();
			String text = ScriptText() + "\r\n// msg_hello MSG_HELLO_2 xMSG_HELLO";

			RenameResult result = this._service.Rename(Request(info, info.FindMacro("MSG_HELLO")!, "MSG_HI", new RefactorDocument(Script, text)));

			String renamed = Apply(result, Script, text);
			Assert.Contains("#define MSG_HI     100", renamed);
			Assert.Contains("   Reply(MSG_HI);", renamed);
			Assert.EndsWith("// msg_hello MSG_HELLO_2 xMSG_HELLO", renamed);
		}

		[Fact]
		public void Rename_Macro_LongerNameKeepsTheValueColumn()
		{
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.FindMacro("MSG_HELLO")!, "MSG_HELLO_WORLD"));

			Assert.Contains("#define MSG_HELLO_WORLD 100", Apply(result, Script, ScriptText()));
		}

		[Fact]
		public void Rename_MacroWithParameters_AdjustsAfterTheParameterList()
		{
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.FindMacro("NOptionX")!, "NOpt"));

			Assert.Contains("#define NOpt(a)       (a)", Apply(result, Script, ScriptText()));
		}

		[Fact]
		public void Rename_RejectedName_ProducesNoEdits()
		{
			ProgramInfo info = CreateInfo();

			RenameResult result = this._service.Rename(Request(info, info.GetProcedure("start")!, "Node001"));

			Assert.False(result.Succeeded);
			Assert.Equal(RenameError.NameExists, result.Validation.Error);
			Assert.Empty(result.Files);
		}

		[Fact]
		public void PreviewMacroRename_ListsOccurrencesWithTheirLines()
		{
			ProgramInfo info = CreateInfo();
			String header = TestScripts.Crlf("#define OTHER 1", "x := MSG_HELLO;   ", "MSG_HELLO");
			ScriptMacro macro = info.FindMacro("MSG_HELLO")!;

			MacroRenamePreview preview = this._service.PreviewMacroRename(
				macro,
				"MSG_HI",
				new[] { new RefactorDocument(Script, ScriptText(), true), new RefactorDocument(Header, header), new RefactorDocument(@"C:\mod\empty.ssl", "nothing here") });

			Assert.Equal(2, preview.Documents.Count);
			Assert.Equal(new[] { 3, 18, 2, 3 }, preview.Matches.Select(m => m.Line).ToArray());
			PreviewMatch define = preview.Matches[0];
			Assert.Equal("MSG_HELLO  100", define.PreviewText);
			Assert.True(define.IsOpenDocument);
			Assert.Equal(new TextSpan(ScriptText().IndexOf("MSG_HELLO", StringComparison.Ordinal), 9), define.Span);
			Assert.Equal("MSG_HELLO;", preview.GetMatches(Header).First().PreviewText);
			Assert.Equal("MSG_HELLO", preview.GetMatches(Header).Last().PreviewText);
		}

		[Fact]
		public void ApplyMacroRename_OnlyRenamesTheSelectedOccurrences()
		{
			ProgramInfo info = CreateInfo();
			String header = TestScripts.Crlf("x := MSG_HELLO;", "y := MSG_HELLO;");
			MacroRenamePreview preview = this._service.PreviewMacroRename(
				info.FindMacro("MSG_HELLO")!,
				"MSG_HI",
				new[] { new RefactorDocument(Script, ScriptText()), new RefactorDocument(Header, header) });
			PreviewMatch[] selected = preview.Matches.Where(m => !(m.FilePath == Header && m.Line == 2)).ToArray();

			RenameResult result = this._service.ApplyMacroRename(preview, selected);

			Assert.Equal(TestScripts.Crlf("x := MSG_HI;", "y := MSG_HELLO;"), Apply(result, Header, header));
			Assert.Contains("#define MSG_HI     100", Apply(result, Script, ScriptText()));
		}

		[Fact]
		public void ApplyMacroRename_WithoutTheDefineOccurrence_DoesNotRealign()
		{
			ProgramInfo info = CreateInfo();
			MacroRenamePreview preview = this._service.PreviewMacroRename(info.FindMacro("MSG_HELLO")!, "MSG_HI", new[] { new RefactorDocument(Script, ScriptText()) });

			RenameResult result = this._service.ApplyMacroRename(preview, preview.Matches.Where(m => m.Line != 3));

			String renamed = Apply(result, Script, ScriptText());
			Assert.Contains("#define MSG_HELLO  100", renamed);
			Assert.Contains("Reply(MSG_HI);", renamed);
		}

		[Fact]
		public void ApplyMacroRename_IgnoresMatchesThatAreNotFromThePreview()
		{
			ProgramInfo info = CreateInfo();
			MacroRenamePreview preview = this._service.PreviewMacroRename(info.FindMacro("MSG_HELLO")!, "MSG_HI", new[] { new RefactorDocument(Script, ScriptText()) });
			var foreign = new PreviewMatch(Script, false, new TextSpan(0, 5), 1, "foreign");

			RenameResult result = this._service.ApplyMacroRename(preview, new[] { foreign });

			Assert.Empty(result.Files);
		}
	}
}
