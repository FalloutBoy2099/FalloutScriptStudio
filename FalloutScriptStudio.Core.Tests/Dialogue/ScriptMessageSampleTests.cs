namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The check against the real scripts of FO2tweaksMk2: global scripts that show game messages, never their own message file.</summary>
	public class ScriptMessageSampleTests
	{
		private static String SourceFolder => Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");

		private static IEnumerable<String> Scripts()
		{
			return Directory.Exists(SourceFolder) ? Directory.GetFiles(SourceFolder, "gl_g_*.ssl").Order() : Enumerable.Empty<String>();
		}

		private static ScriptMessageScan Scan(String path)
		{
			String text = File.ReadAllText(path);
			IncludeResolver resolver = new IncludeResolver(Path.Combine(SourceFolder, "headers"), SourceFolder);
			SortedDictionary<String, ScriptMacro> macros = new MacroCollector(resolver).CollectFromText(text, path);
			ProgramInfo info = new InternalParser().Parse(text, path).WithScriptMacros(macros);
			return ScriptMessageScanner.Scan(text, info, new DialogueFunctionRules());
		}

		[Fact]
		public void The_sample_scripts_are_there()
		{
			Assert.True(Scripts().Count() >= 20);
		}

		[Fact]
		public void No_sample_script_uses_a_message_of_its_own_message_file()
		{
			foreach (String path in Scripts())
			{
				ScriptMessageScan scan = Scan(path);

				Assert.True(scan.OwnUsages.Count() == 0, Path.GetFileName(path) + " reports " + String.Join(", ", scan.OwnUsages));
				Assert.True(scan.UnresolvedCount == 0, Path.GetFileName(path) + " has unresolved uses");
			}
		}

		[Fact]
		public void The_messages_of_another_message_file_are_checked_against_that_file()
		{
			String path = Path.Combine(SourceFolder, "gl_g_highlighting.ssl");
			ScriptMessageScan scan = Scan(path);
			Assert.Equal(new[] { 101 }, scan.OtherFileNumbers);

			Dictionary<Int32, MessageDocument> complete = new Dictionary<Int32, MessageDocument> { [101] = MessageDocument.Parse(new[] { "{17}{}{On}", "{18}{}{Off}" }) };
			Assert.Empty(ScriptMessageConsistency.Check(scan, null, complete).Findings);

			Dictionary<Int32, MessageDocument> incomplete = new Dictionary<Int32, MessageDocument> { [101] = MessageDocument.Parse(new[] { "{17}{}{On}" }) };
			ScriptMessageFinding missing = Assert.Single(ScriptMessageConsistency.Check(scan, null, incomplete).Findings);
			Assert.Equal(ScriptMessageFindingKind.MissingMessage, missing.Kind);
			Assert.Equal(101, missing.FileNumber);
			Assert.Equal(18, missing.Number);
		}
	}
}