namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Editor.Support;

	internal static class EditorSessionTestScripts
	{
		internal const String Script =
			"#include \"..\\headers\\define.h\"\r\n"
			+ "\r\n"
			+ "procedure start;\r\n"
			+ "procedure other;\r\n"
			+ "\r\n"
			+ "variable begin\r\n"
			+ "   count;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "   count := 1;\r\n"
			+ "   other;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure other begin\r\n"
			+ "   count := 2;\r\n"
			+ "end\r\n";

		internal static DocumentViewModel CreateScriptWithVariable(EditorTestEnvironment env)
		{
			DocumentViewModel document = env.CreateDocument(Script);
			ScriptVariable variable = new ScriptVariable("count", VariableScope.Global) { DeclaredFile = document.FilePath, DeclaredLine = 7 };
			document.ProgramInfo = new ProgramInfo(document.ProgramInfo!.Procedures, new[] { variable }, null, null, document.FilePath);
			return document;
		}
	}
}