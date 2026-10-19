namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Highlighting;
	using Xunit;

	public sealed class HighlightingTests
	{
		private const String Script =
			"#define LOCAL_MACRO 1\r\n"
			+ "procedure start;\r\n"
			+ "procedure helper_proc;\r\n"
			+ "procedure start begin\r\n"
			+ "   call helper_proc;\r\n"
			+ "   x := LOCAL_MACRO + CUSTOM_WORD;\r\n"
			+ "end\r\n"
			+ "procedure helper_proc begin\r\n"
			+ "end\r\n";

		private static String? ColorNameOf(EditorSession session, Int32 line, String word)
		{
			DocumentHighlighter highlighter = new DocumentHighlighter(session.Context.Document, session.Highlighting.Definition!);
			HighlightedLine highlighted = highlighter.HighlightLine(line);
			Int32 offset = session.Context.Document.GetLineByNumber(line).Offset;
			Int32 start = session.Context.Document.GetText(session.Context.Document.GetLineByNumber(line)).IndexOf(word, StringComparison.Ordinal) + offset;
			return highlighted.Sections.LastOrDefault(s => s.Offset <= start && start < s.Offset + s.Length)?.Color?.Name;
		}

		private static DocumentViewModel CreateDocument(EditorTestEnvironment env)
		{
			DocumentViewModel document = env.CreateDocument(Script);
			document.ProgramInfo = document.ProgramInfo!.WithScriptMacros(new Dictionary<String, ScriptMacro>
			{
				["LOCAL_MACRO"] = new ScriptMacro("LOCAL_MACRO", "LOCAL_MACRO", "1", "C:\\scripts\\test.ssl", 1, null),
				["HEADER_MACRO"] = new ScriptMacro("HEADER_MACRO", "HEADER_MACRO", "2", "C:\\headers\\define.h", 3, null),
			});
			return document;
		}

		[Fact]
		public void UserProcedureNames_GetTheProcedureColor_StandardOnesKeepTheirs()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession session = env.StartSession(CreateDocument(env));

				Assert.Equal("ProcedureNames", ColorNameOf(session, 5, "helper_proc"));
				Assert.Equal("StandardProcedures", ColorNameOf(session, 4, "start"));
			});
		}

		[Fact]
		public void MacrosOfTheScript_AreColoredButHeaderMacrosAreNot()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession session = env.StartSession(CreateDocument(env));

				Assert.Equal("UserMacros", ColorNameOf(session, 6, "LOCAL_MACRO"));
				Assert.NotEqual("UserMacros", ColorNameOf(session, 6, "CUSTOM_WORD"));
			});
		}

		[Fact]
		public void UserMacroSource_ChangesAreApplied()
		{
			UiThread.Run(() =>
			{
				FakeUserMacroSource macros = new FakeUserMacroSource();
				using EditorTestEnvironment env = new EditorTestEnvironment(false, null, macros);
				using EditorSession session = env.StartSession(CreateDocument(env));
				Assert.NotEqual("UserMacros", ColorNameOf(session, 6, "CUSTOM_WORD"));

				macros.Set("CUSTOM_WORD");
				UiThread.Flush();

				Assert.Equal("UserMacros", ColorNameOf(session, 6, "CUSTOM_WORD"));
			});
		}

		[Fact]
		public void NewProcedure_IsColoredAfterTheNextParse()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   call fresh_one;\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				Assert.NotEqual("ProcedureNames", ColorNameOf(session, 2, "fresh_one"));

				document.TextDocument.Insert(document.TextDocument.TextLength, "procedure fresh_one begin\r\nend\r\n");
				document.ProgramInfo = new InternalParser().Parse(document.TextDocument.Text, document.FilePath);

				Assert.Equal("ProcedureNames", ColorNameOf(session, 2, "fresh_one"));
			});
		}

		[Fact]
		public void MessageDocument_UsesTheMessageDefinition()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("# note\r\n{100}{}{Hi ::smile:: there}\r\n", "C:\\text\\a.msg");
				using EditorSession session = env.StartSession(document);

				Assert.Equal("Message", session.Highlighting.Definition!.Name);
				Assert.Equal("LineComment", ColorNameOf(session, 1, "# note"));
				Assert.Equal("Emotion", ColorNameOf(session, 2, "::smile::"));
			});
		}
	}
}