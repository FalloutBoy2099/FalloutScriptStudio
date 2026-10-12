namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Tests.TextTools;

	/// <summary>Realistic dialogue scripts, macros and message files for the dialogue tests.</summary>
	internal static class DialogueSamples
	{
		public const String ScriptPath = @"C:\mod\scripts\dcjohn.ssl";

		/// <summary>The macros the sample script defines (the way the macro collector would report them).</summary>
		public static SortedDictionary<String, ScriptMacro> Macros()
		{
			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
			void Add(String token, String definition, String code, Int32 line)
			{
				macros[token] = new ScriptMacro(token, definition, code, ScriptPath, line, null);
			}

			Add("NAME", "NAME", "SCRIPT_JOHN", 3);
			Add("SCRIPT_JOHN", "SCRIPT_JOHN", "123", 4);
			Add("MSG_HELLO", "MSG_HELLO", "100", 5);
			Add("MSG_BYE", "MSG_BYE", "(MSG_HELLO + 1)", 6);
			Add("LOOP", "LOOP", "LOOP", 7);
			Add("mstr", "mstr(x)", "message_str(NAME, x)", 8);
			return macros;
		}

		/// <summary>The sample script: a start node, nodes with every kind of statement, an exit node and an unused node.</summary>
		public static String Script()
		{
			return TestScripts.Crlf(
				"// Dialogue of John",                                  // 1
				"#include \"..\\headers\\define.h\"",                  // 2
				"#define NAME SCRIPT_JOHN",                            // 3
				"#define SCRIPT_JOHN 123",                             // 4
				"#define MSG_HELLO 100",                              // 5
				"#define MSG_BYE (MSG_HELLO + 1)",                    // 6
				"#define LOOP LOOP",                                  // 7
				"#define mstr(x) message_str(NAME, x)",               // 8
				"",                                                    // 9
				"procedure start;",                                    // 10
				"procedure talk_p_proc;",                              // 11
				"procedure Node001;",                                  // 12
				"procedure Node002;",                                  // 13
				"procedure Node999;",                                  // 14
				"procedure Node777;",                                  // 15
				"",                                                    // 16
				"procedure start begin",                               // 17
				"end",                                                 // 18
				"",                                                    // 19
				"procedure talk_p_proc begin",                         // 20
				"   Reply(MSG_HELLO);",                                // 21
				"   NOption(101, Node001, 4);",                        // 22
				"   NLowOption(102, Node999);",                        // 23
				"   if (global_var(3) == 1) then begin",               // 24
				"      GOption(MSG_BYE, Node002, 5);",                 // 25
				"   end else begin",                                   // 26
				"      BOption(104, Node999, -1);",                    // 27
				"   end",                                              // 28
				"end",                                                 // 29
				"",                                                    // 30
				"procedure Node001 begin",                             // 31
				"   Reply(mstr(110));",                                // 32
				"   NMessage(111);",                                   // 33
				"   gsay_reply(NAME, 112);",                           // 34
				"   gsay_message(NAME, 113, 50);",                     // 35
				"   gsay_option(NAME, 114, Node002, 50);",             // 36
				"   giq_option(4, NAME, 115, Node999, 50);",           // 37
				"   call Node002;",                                    // 38
				"end",                                                 // 39
				"",                                                    // 40
				"procedure Node002 begin",                             // 41
				"   Reply(120);",                                      // 42
				"   NOption(121, Node999, 4);",                        // 43
				"end",                                                 // 44
				"",                                                    // 45
				"procedure Node999 begin",                             // 46
				"end",                                                 // 47
				"",                                                    // 48
				"procedure Node777 begin",                             // 49
				"   Reply(130);",                                      // 50
				"end");                                                // 51
		}

		/// <summary>Parses the procedures of a script text and attaches macros.</summary>
		public static ProgramInfo CreateInfo(String? text = null)
		{
			return new InternalParser().Parse(text ?? Script(), ScriptPath).WithScriptMacros(Macros());
		}

		/// <summary>The message file of the script.</summary>
		public static MessageDocument OwnMessages()
		{
			return MessageDocument.Parse(new[]
			{
				"# John",
				"{100}{}{Hello there, stranger.}",
				"\t{101}{}{I need your help.}",
				"\t{102}{}{Goodbye.}",
				"{110}{}{Reply from message_str.}",
				"{111}{}{A message.}",
				"{120}{}{Second reply.}",
				"\t{121}{}{Leave.}",
				"{130}{}{Nobody reaches this.}",
			});
		}

		/// <summary>The message file with the number 123 of scripts.lst.</summary>
		public static MessageDocument OtherMessages()
		{
			return MessageDocument.Parse(new[]
			{
				"{110}{}{Reply from message_str.}",
				"{112}{}{Reply from gsay_reply.}",
				"{113}{}{Message from gsay_message.}",
				"{114}{}{Option from gsay_option.}",
				"{115}{}{Option from giq_option.}",
			});
		}

		/// <summary>A message source holding the sample messages: the own file and file number 123.</summary>
		public static FakeMessageSource Messages()
		{
			var source = new FakeMessageSource();
			source.SetFile(-1, OwnMessages(), @"C:\mod\text\english\dialog\dcjohn.msg");
			source.SetFile(123, OtherMessages(), @"C:\mod\text\english\dialog\other.msg");
			return source;
		}

		public static IReadOnlyList<DialogueLine> Parse(String nodeCode, DialogueNodeParseOptions? options = null, DialogueFunctionRules? rules = null)
		{
			return new DialogueNodeParser(rules ?? new DialogueFunctionRules()).ParseNode(nodeCode, CreateInfo(), options ?? new DialogueNodeParseOptions());
		}
	}

	/// <summary>An in-memory message source that records which files were requested.</summary>
	internal sealed class FakeMessageSource : IDialogueMessageSource
	{
		private readonly Dictionary<Int32, (MessageDocument Document, String Path)> _files = new Dictionary<Int32, (MessageDocument, String)>();

		public List<Int32> Requested { get; } = new List<Int32>();

		public void SetFile(Int32 number, MessageDocument document, String path)
		{
			this._files[number > 0 ? number : -1] = (document, path);
		}

		public Task EnsureLoadedAsync(IEnumerable<Int32> messageFileNumbers, CancellationToken cancellationToken = default)
		{
			this.Requested.AddRange(messageFileNumbers.Where(n => n > 0));
			return Task.CompletedTask;
		}

		public DialogueMessageLookup GetMessage(Int32 messageFileNumber, Int32 messageNumber)
		{
			Int32 key = messageFileNumber > 0 ? messageFileNumber : -1;
			if (messageNumber <= 0)
			{
				return new DialogueMessageLookup(DialogueMessageStatus.NoMessageNumber, null, null);
			}

			if (!this._files.TryGetValue(key, out (MessageDocument Document, String Path) file))
			{
				return new DialogueMessageLookup(DialogueMessageStatus.FileNotFound, String.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFileTexts.FileNotFoundFormat, messageFileNumber), null);
			}

			return file.Document.TryGetText(messageNumber, out String text)
				? new DialogueMessageLookup(DialogueMessageStatus.Found, text, file.Path)
				: new DialogueMessageLookup(DialogueMessageStatus.MessageNotFound, MessageFileTexts.TextNotFound, file.Path);
		}

		public String? GetMessageFilePath(Int32 messageFileNumber)
		{
			return this._files.TryGetValue(messageFileNumber > 0 ? messageFileNumber : -1, out (MessageDocument Document, String Path) file) ? file.Path : null;
		}
	}
}
