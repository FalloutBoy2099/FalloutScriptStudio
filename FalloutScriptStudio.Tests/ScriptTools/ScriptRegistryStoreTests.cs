namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using Xunit;

	public sealed class ScriptRegistryStoreTests
	{
		private static readonly String Ha = ((Char)0x0425).ToString();
		private static readonly String Rest = new String(new[] { (Char)0x0440, (Char)0x0430, (Char)0x043C });
		private static readonly String Test = new String(new[] { (Char)0x0422, (Char)0x0435, (Char)0x0441, (Char)0x0442 });
		private static readonly String Bom = ((Char)0xFEFF).ToString();

		// A letter with a diacritic in the Windows ANSI code page of this machine (0xE9 is e-acute in 1252, a Cyrillic letter in 1251).
		private static readonly String Accent = TextEncodings.Ansi.GetString(new Byte[] { 0xE9 });

		private readonly ScriptRegistryStore _store = new ScriptRegistryStore();

		private static String ListLine(String script, String description, Int32 vars)
		{
			return new ScriptListEntry { Script = script, Description = description, LocalVars = vars }.ToListLine();
		}

		[Fact]
		public void Load_ReadsEntriesAndGameNames()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("game/scripts/scripts.lst", ListLine("a.int", "First", 1) + "\r\n" + ListLine("b.int", "Second", 2) + "\r\n");
			String names = temp.Write(
				"game/text/english/game/scrname.msg",
				"#\r\n# comment\r\n#\r\n{101}{}{Alpha}  # a.int ; First\r\n{102}{lip}{ Beta}  # b.int ; Second\r\n{500}{}{Far}\r\n{1}{}{Low}\r\nnot a message\r\n");

			ScriptRegistry registry = this._store.Load(new ScriptRegistryPaths(list, names, null), TextEncodings.Ansi);

			Assert.Equal(2, registry.Entries.Count);
			Assert.Equal("a.int", registry.Entries[0].Script);
			Assert.Equal("First", registry.Entries[0].Description);
			Assert.Equal("Alpha", registry.Entries[0].GameName);
			Assert.Equal("lip", registry.Entries[1].Lip);
			Assert.Equal(" Beta", registry.Entries[1].GameName);
		}

		[Fact]
		public void Load_WithoutMessageFile_LeavesGameNamesEmpty()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("scripts.lst", ListLine("a.int", "First", 1) + "\r\n");

			ScriptRegistry registry = this._store.Load(new ScriptRegistryPaths(list, null, null), TextEncodings.Ansi);

			Assert.Single(registry.Entries);
			Assert.Equal(String.Empty, registry.Entries[0].GameName);
		}

		[Fact]
		public void Save_ThenLoad_RoundTripsListAndNames()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("scripts.lst", String.Empty);
			String names = temp.Write("scrname.msg", String.Empty);
			ScriptRegistryPaths paths = new ScriptRegistryPaths(list, names, null);
			ScriptRegistry registry = new ScriptRegistry(TextEncodings.Ansi);
			registry.Entries.Add(new ScriptListEntry { Script = "a.int", Description = "First", LocalVars = 3, GameName = "Alpha" });
			registry.Entries.Add(new ScriptListEntry { Script = "b.int", Description = "Second; with semicolon", LocalVars = 0, GameName = "Beta", Lip = "x" });

			this._store.Save(registry, paths, TextEncodings.Ansi);
			ScriptRegistry loaded = this._store.Load(paths, TextEncodings.Ansi);

			Assert.Equal(2, loaded.Entries.Count);
			Assert.Equal("Second; with semicolon", loaded.Entries[1].Description);
			Assert.Equal(3, loaded.Entries[0].LocalVars);
			Assert.Equal("Alpha", loaded.Entries[0].GameName);
			Assert.Equal("x", loaded.Entries[1].Lip);
			Assert.Equal("Beta", loaded.Entries[1].GameName);
		}

		[Fact]
		public void Save_WritesTheLegacyFormats()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("scripts.lst", String.Empty);
			String names = temp.Write("scrname.msg", String.Empty);
			ScriptRegistry registry = new ScriptRegistry(TextEncodings.Ansi);
			registry.Entries.Add(new ScriptListEntry { Script = "a.int", Description = "First", LocalVars = 3, GameName = "Alpha" });

			this._store.Save(registry, new ScriptRegistryPaths(list, names, null), TextEncodings.Ansi);

			Assert.Equal("a.int".PadRight(16) + "; " + "First".PadRight(45) + " # local_vars=3\r\n", File.ReadAllText(list));
			Assert.Equal(
				"#\r\n#   This file was built using Sfall Script Editor.\r\n#\r\n" + "{101}{}{Alpha}".PadRight(60) + "# " + "a.int".PadRight(16) + "; First\r\n",
				File.ReadAllText(names));
		}

		[Fact]
		public void Save_Cp866_ReplacesTheCyrillicHaInNames()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("scripts.lst", String.Empty);
			String names = temp.Write("scrname.msg", String.Empty);
			ScriptRegistry registry = new ScriptRegistry(TextEncodings.Ansi);
			registry.Entries.Add(new ScriptListEntry { Script = "a.int", GameName = Ha + Rest });

			this._store.Save(registry, new ScriptRegistryPaths(list, names, null), TextEncodings.Oem866);

			String text = TextEncodings.Oem866.GetString(File.ReadAllBytes(names));
			Assert.Contains("{X" + Rest + "}", text, StringComparison.Ordinal);
		}

		[Fact]
		public void Load_AnsiList_IsWrittenBackAsAnsi()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("scripts.lst", ListLine("a.int", "caf" + Accent, 0) + "\r\n", TextEncodings.Ansi);
			ScriptRegistryPaths paths = new ScriptRegistryPaths(list, null, null);

			ScriptRegistry registry = this._store.Load(paths, TextEncodings.Ansi);
			this._store.Save(registry, paths, TextEncodings.Ansi);

			Assert.Equal("caf" + Accent, registry.Entries[0].Description);
			Assert.Equal(registry.ListEncoding.CodePage, TextEncodings.Ansi.CodePage);
			Assert.True(ContainsSequence(File.ReadAllBytes(list), TextEncodings.Ansi.GetBytes("caf" + Accent)));
		}

		[Fact]
		public void Load_Utf8ListWithBom_IsWrittenBackAsUtf8WithoutBom()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String list = temp.Write("scripts.lst", Bom + ListLine("a.int", Test, 0) + "\r\n");
			ScriptRegistryPaths paths = new ScriptRegistryPaths(list, null, null);

			ScriptRegistry registry = this._store.Load(paths, TextEncodings.Ansi);
			this._store.Save(registry, paths, TextEncodings.Ansi);

			Byte[] bytes = File.ReadAllBytes(list);
			Assert.Equal(Test, registry.Entries[0].Description);
			Assert.NotEqual(0xEF, bytes[0]);
			Assert.Contains(Test, new UTF8Encoding(false).GetString(bytes), StringComparison.Ordinal);
		}

		[Fact]
		public void FormatDefine_UsesTheLegacyColumns()
		{
			ScriptListEntry entry = new ScriptListEntry { Script = "abc.int", Description = "The test" };

			String line = ScriptRegistryStore.FormatDefine("script_abc", 12, entry);

			Assert.Equal("#define SCRIPT_ABC".PadRight(32) + "(12)".PadRight(8) + "// " + "abc.int".PadRight(16) + "; The test", line);
		}

		[Fact]
		public void InsertDefine_GoesBeforeTheLastDirectiveLine()
		{
			List<String> lines = ["#ifndef SCRIPTS_H", "#define SCRIPTS_H", "", "#define SCRIPT_OLD (1)", "", "#endif"];
			ScriptListEntry entry = new ScriptListEntry { Script = "new.int", Description = "New" };

			DefineInsertResult result = ScriptRegistryStore.InsertDefine(lines, "SCRIPT_NEW", 2, entry);

			Assert.Equal(DefineInsertResult.Added, result);
			Assert.Equal(7, lines.Count);
			Assert.Equal(ScriptRegistryStore.FormatDefine("SCRIPT_NEW", 2, entry), lines[5]);
			Assert.Equal("#endif", lines[6]);
		}

		[Fact]
		public void InsertDefine_ExistingDefinition_IsNotDuplicated()
		{
			List<String> lines = ["#define SCRIPT_NEW (2)", "#endif"];

			DefineInsertResult result = ScriptRegistryStore.InsertDefine(lines, "script_new", 2, new ScriptListEntry { Script = "new.int" });

			Assert.Equal(DefineInsertResult.AlreadyDefined, result);
			Assert.Equal(2, lines.Count);
		}

		[Fact]
		public void InsertDefine_FileWithoutDirectives_InsertsBeforeTheFirstLine()
		{
			List<String> lines = ["// nothing here", ""];

			DefineInsertResult result = ScriptRegistryStore.InsertDefine(lines, "SCRIPT_A", 1, new ScriptListEntry { Script = "a.int" });

			Assert.Equal(DefineInsertResult.Added, result);
			Assert.StartsWith("#define SCRIPT_A", lines[0], StringComparison.Ordinal);
		}

		[Fact]
		public void InsertDefine_EmptyFile_AppendsTheLine()
		{
			List<String> lines = [];

			DefineInsertResult result = ScriptRegistryStore.InsertDefine(lines, "SCRIPT_A", 1, new ScriptListEntry { Script = "a.int" });

			Assert.Equal(DefineInsertResult.Added, result);
			Assert.Single(lines);
		}

		[Fact]
		public void AddDefine_KeepsAnAnsiHeaderAnsi()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String header = temp.Write("scripts.h", "// caf" + Accent + "\r\n#define SCRIPT_OLD (1)\r\n#endif\r\n", TextEncodings.Ansi);

			DefineInsertResult result = this._store.AddDefine(header, "SCRIPT_A", 2, new ScriptListEntry { Script = "a.int", Description = "A" });

			Assert.Equal(DefineInsertResult.Added, result);
			String text = TextEncodings.Ansi.GetString(File.ReadAllBytes(header));
			Assert.Contains("caf" + Accent, text, StringComparison.Ordinal);
			Assert.Contains("#define SCRIPT_A", text, StringComparison.Ordinal);
			Assert.EndsWith("#endif\r\n", text, StringComparison.Ordinal);
		}

		[Fact]
		public void AddDefine_KeepsTheByteOrderMarkOfAUtf8Header()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String header = temp.Write("scripts.h", Bom + "#define SCRIPT_OLD (1)\r\n#endif\r\n");

			this._store.AddDefine(header, "SCRIPT_A", 2, new ScriptListEntry { Script = "a.int" });

			Byte[] bytes = File.ReadAllBytes(header);
			Assert.Equal(0xEF, bytes[0]);
			Assert.Equal(0xBB, bytes[1]);
			Assert.Equal(0xBF, bytes[2]);
		}

		[Fact]
		public void ApplyGameNames_IgnoresLinesWithBraceInTheText()
		{
			List<ScriptListEntry> entries = [new ScriptListEntry { Script = "a.int" }];

			ScriptRegistryStore.ApplyGameNames(entries, new[] { "{101}{}{Na}me}  # a.int ; x" });

			Assert.Equal(String.Empty, entries[0].GameName);
		}

		private static Boolean ContainsSequence(Byte[] all, Byte[] needle)
		{
			return all.AsSpan().IndexOf(needle) >= 0;
		}
	}
}