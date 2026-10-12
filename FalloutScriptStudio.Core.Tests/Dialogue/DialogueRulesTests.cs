namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Xunit;

	public class DialogueRulesTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		[Fact]
		public void BuiltInRules_DescribeTheEngineFunctions()
		{
			var rules = new DialogueFunctionRules();

			Assert.True(rules.TryGet("GIQ_OPTION", out DialogueFunctionRule? giq));
			Assert.Equal((DialogueOpcode.GiqOption, 5, 2, 1, 3, 0), (giq.Opcode, giq.TotalArgs, giq.MessageArg, giq.MessageFileArg, giq.NodeArg, giq.IqArg));
			Assert.True(giq.IsDefault);
			Assert.True(rules.TryGet("gsay_reply", out DialogueFunctionRule? reply));
			Assert.Equal((1, 0, -1, -1), (reply.MessageArg, reply.MessageFileArg, reply.NodeArg, reply.IqArg));
			Assert.Equal(4, rules.Rules.Count);
			Assert.Empty(rules.UserRules);
			Assert.False(rules.TryGet("missing", out _));
		}

		[Fact]
		public void UserRules_ComeFirst_AndCannotReplaceBuiltInOnes()
		{
			var rules = new DialogueFunctionRules(new[]
			{
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "MyOption", 1, 0, 2, 3, 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "myoption", 2, 0, 3, 0, 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "gsay_reply", 9, 0, 0, 0, 9),
			});

			Assert.Equal(new[] { "MyOption", "gsay_reply", "gsay_message", "gsay_option", "giq_option" }, rules.Rules.Select(r => r.Name).ToArray());
			Assert.Single(rules.UserRules);
			Assert.True(rules.TryGet("MYOPTION", out DialogueFunctionRule? mine));
			Assert.Equal(0, mine.MessageArg);
			Assert.True(rules.TryGet("gsay_reply", out DialogueFunctionRule? builtIn));
			Assert.Equal(2, builtIn.TotalArgs);
		}

		[Fact]
		public void RulesFile_ParsesTheLegacyFormat()
		{
			String[] lines =
			{
				";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ",
				"Option, NOptionX, 3, 1, 0, 2, 3",
				"  Reply, SayIt, 2, 2, 1, 0, 0",
				"Message, Shout, 1, 1, 0, 0, 0",
				"Call, GotoNode, 1, 0, 0, 1, 0",
				"",
				"Bogus, Nope, 1, 1, 0, 0, 0",
				"Option, Short, 1, 1",
				"Option, BadNumber, x, 1, 0, 0, 0",
				"Option, ZeroArgs, 0, 1, 0, 0, 0",
				"Option, , 1, 1, 0, 0, 0",
			};

			var rules = DialogueFunctionRulesFile.Parse(lines);

			Assert.Equal(new[] { "NOptionX", "SayIt", "Shout", "GotoNode" }, rules.Select(r => r.Name).ToArray());
			DialogueFunctionRule option = rules[0];
			Assert.Equal((DialogueOpcode.Option, 3, 0, -1, 1, 2), (option.Opcode, option.TotalArgs, option.MessageArg, option.MessageFileArg, option.NodeArg, option.IqArg));
			Assert.Equal(DialogueOpcode.CallMacro, rules[3].Opcode);
			Assert.All(rules, r => Assert.False(r.IsDefault));
		}

		[Fact]
		public void RulesFile_FormatRoundTrips_AndSkipsBuiltInRules()
		{
			var rules = new DialogueFunctionRules(new[]
			{
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOptionX", 1, 0, 2, 3, 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.CallMacro, "GotoNode", 0, 0, 1, 0, 1),
			});

			var lines = DialogueFunctionRulesFile.Format(rules.Rules);

			Assert.Equal(
				new[]
				{
					";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ",
					"Option, NOptionX, 3, 1, 0, 2, 3",
					"Call, GotoNode, 1, 0, 0, 1, 0",
				},
				lines.ToArray());
			Assert.Equal(rules.UserRules.Select(r => r.Name).ToArray(), DialogueFunctionRulesFile.Parse(lines).Select(r => r.Name).ToArray());
		}

		[Fact]
		public void RulesFile_Format_KeepsCommentsAndUnknownLinesInTheirPlace()
		{
			String[] existing =
			{
				";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ",
				"; my own notes",
				"Option, NOptionX, 3, 1, 0, 2, 3",
				"this line is not a rule",
				"",
				"Call, GotoNode, 1, 0, 0, 1, 0",
				"Option, Removed, 1, 1, 0, 0, 0",
				"Option, Short, 1, 1",
			};
			var rules = new DialogueFunctionRules(new[]
			{
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOptionX", 2, 0, 3, 3, 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.CallMacro, "GotoNode", 0, 0, 1, 0, 1),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Added", 1, 0, 0, 0, 1),
			});

			var lines = DialogueFunctionRulesFile.Format(rules.Rules, existing);

			Assert.Equal(
				new[]
				{
					";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ",
					"; my own notes",
					"Option, NOptionX, 3, 2, 0, 3, 3",
					"this line is not a rule",
					"",
					"Call, GotoNode, 1, 0, 0, 1, 0",
					"Option, Short, 1, 1",
					"Reply, Added, 1, 1, 0, 0, 0",
				},
				lines.ToArray());
		}

		[Fact]
		public async Task Service_Save_KeepsTheCommentsOfTheFile()
		{
			String path = this._folder.Combine("DialogFunc.cfg");
			File.WriteAllText(path, ";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ\r\n; keep me\r\nOption, NOptionX, 3, 1, 0, 2, 3\r\nbroken line\r\n");
			var service = new DialogueRulesService(path);

			await service.SaveAsync(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOptionX", 1, 0, 2, 3, 3), DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Say", 1, 0, 0, 0, 1) });

			Assert.Equal(
				new[] { ";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ", "; keep me", "Option, NOptionX, 3, 1, 0, 2, 3", "broken line", "Reply, Say, 1, 1, 0, 0, 0" },
				File.ReadAllLines(path));
		}

		[Fact]
		public async Task Service_Save_NeverOverwritesAFileThatCouldNotBeRead()
		{
			String path = this._folder.Combine("DialogFunc.cfg");
			File.WriteAllText(path, "Option, NOptionX, 3, 1, 0, 2, 3\r\n");
			var service = new DialogueRulesService(path);
			DialogueFunctionRules loaded;
			using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				loaded = await service.LoadAsync();

				Assert.Empty(loaded.UserRules);
				await Assert.ThrowsAsync<IOException>(() => service.SaveAsync(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Say", 1, 0, 0, 0, 1) }));
			}

			Assert.Equal("Option, NOptionX, 3, 1, 0, 2, 3\r\n", File.ReadAllText(path));

			await service.SaveAsync(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOptionX", 1, 0, 2, 3, 3) });
			Assert.Contains("Option, NOptionX, 3, 1, 0, 2, 3", File.ReadAllText(path));
		}

		[Fact]
		public void OpcodeNames_MapToTheRuleKinds()
		{
			Assert.Equal(DialogueOpcode.Option, DialogueOpcodeExtensions.ParseRuleKind("Option"));
			Assert.Equal(DialogueOpcode.CallMacro, DialogueOpcodeExtensions.ParseRuleKind(" Call "));
			Assert.Equal(DialogueOpcode.None, DialogueOpcodeExtensions.ParseRuleKind("option"));
			Assert.Equal("Call", DialogueOpcode.CallMacro.ToRuleKind());
			Assert.Equal("Reply", DialogueOpcode.Reply.ToRuleKind());
			Assert.True(DialogueOpcode.GsayOption.IsOption());
			Assert.True(DialogueOpcode.GsayMessage.IsNpcText());
			Assert.True(DialogueOpcode.Call.IsTransition());
			Assert.False(DialogueOpcode.Reply.IsTransition());
			Assert.True(DialogueOpcode.Reply.HasMessage());
			Assert.False(DialogueOpcode.None.HasMessage());
		}

		[Fact]
		public async Task Service_SavesAndLoadsTheUserRules()
		{
			String path = this._folder.Combine("settings\\DialogFunc.cfg");
			var service = new DialogueRulesService(path);
			Int32 changed = 0;
			service.Changed += (_, _) => changed++;

			await service.SaveAsync(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOptionX", 1, 0, 2, 3, 3) });

			Assert.Equal(new[] { "NOptionX" }, service.Current.UserRules.Select(r => r.Name).ToArray());
			Assert.Contains("Option, NOptionX, 3, 1, 0, 2, 3", File.ReadAllText(path));
			Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));

			var other = new DialogueRulesService(path);
			await other.LoadAsync();
			Assert.Equal(new[] { "NOptionX" }, other.Current.UserRules.Select(r => r.Name).ToArray());
			Assert.Equal(1, changed);
		}

		[Fact]
		public async Task Service_MissingFile_GivesOnlyTheBuiltInRules()
		{
			var service = new DialogueRulesService(this._folder.Combine("nothing.cfg"));

			DialogueFunctionRules rules = await service.LoadAsync();

			Assert.Equal(4, rules.Rules.Count);
			Assert.Same(rules, service.Current);
		}

		[Fact]
		public async Task Service_SavedRulesAreSeenByTheParserRightAway()
		{
			var service = new DialogueRulesService(this._folder.Combine("rules.cfg"));
			var parser = new DialogueNodeParser(service);

			await service.SaveAsync(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Say", 1, 0, 0, 0, 1) });

			Assert.Single(parser.ParseNode("   Say(100);", DialogueSamples.CreateInfo(), new DialogueNodeParseOptions()));
		}
	}
}
