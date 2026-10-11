namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>A small dialogue script and its message file for the flowchart and dialogue preview tests.</summary>
	internal static class FlowchartSamples
	{
		public const String ScriptPath = @"C:\mod\scripts\dcjohn.ssl";

		public const String MessagePath = @"C:\mod\text\english\dialog\dcjohn.msg";

		/// <summary>The sample script: a start node, three linked nodes, an exit node and an unused node.</summary>
		public static String Script()
		{
			return String.Join(
				"\r\n",
				"procedure start;",
				"procedure talk_p_proc;",
				"procedure Node001;",
				"procedure Node002;",
				"procedure Node999;",
				"procedure Node777;",
				"",
				"procedure start begin",
				"end",
				"",
				"procedure talk_p_proc begin",
				"   Reply(100);",
				"   NOption(101, Node001, 4);",
				"   NLowOption(102, Node999);",
				"end",
				"",
				"procedure Node001 begin",
				"   Reply(110);",
				"   NOption(111, Node002, 4);",
				"   call Node999;",
				"end",
				"",
				"procedure Node002 begin",
				"   Reply(120);",
				"   NOption(121, Node999, 4);",
				"end",
				"",
				"procedure Node999 begin",
				"end",
				"",
				"procedure Node777 begin",
				"   Reply(130);",
				"end") + "\r\n";
		}

		/// <summary>The messages of the sample script.</summary>
		public static MessageDocument Messages()
		{
			return MessageDocument.Parse(new[]
			{
				"{100}{}{Hello there.}",
				"{101}{}{Who are you?}",
				"{102}{}{Bye.}",
				"{110}{}{I am John.}",
				"{111}{}{Tell me more.}",
				"{120}{}{More.}",
				"{121}{}{Leave.}",
				"{130}{}{Nobody comes here.}",
			});
		}

		/// <summary>Creates a parsed script document.</summary>
		/// <param name="text">The text, or <see langword="null"/> for <see cref="Script"/>.</param>
		/// <param name="path">The file path.</param>
		/// <returns>The document.</returns>
		public static DocumentViewModel CreateDocument(String? text = null, String path = ScriptPath)
		{
			String source = text ?? Script();
			DocumentViewModel document = new DocumentViewModel(path, DocumentKind.Script, new TextDocument(source));
			document.ProgramInfo = new InternalParser().Parse(source, path);
			document.TextDocument.UndoStack.ClearAll();
			document.IsModified = false;
			return document;
		}

		/// <summary>Creates the message source of the sample messages.</summary>
		/// <returns>The source.</returns>
		public static IDialogueMessageSource CreateSource()
		{
			return new DialogueMessageSource(Messages(), MessagePath);
		}
	}
}
