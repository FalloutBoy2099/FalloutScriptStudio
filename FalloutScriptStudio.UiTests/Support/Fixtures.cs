namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text.Json;
	using FalloutScriptStudio.TestSupport;

	/// <summary>The files the smoke tests create under <c>_test_files\work</c>: a copy of the real sample mod, and a small dialogue mod with its message files and <c>scripts.lst</c>.</summary>
	internal static class Fixtures
	{
		/// <summary>The name of the dialogue script.</summary>
		public const String DialogueScriptName = "dlgtest.ssl";

		/// <summary>Copies the sources of the sample mod FO2tweaksMk2 into a folder.</summary>
		/// <param name="target">The folder to fill.</param>
		/// <returns>The folder of the copy that holds the scripts.</returns>
		public static String CopySampleMod(String target)
		{
			String source = Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");
			String destination = Path.Combine(target, "FO2tweaksMk2", "source");
			CopyFolder(source, destination);
			return destination;
		}

		/// <summary>Gets the path of the sample script the tests open.</summary>
		/// <param name="sourceFolder">The folder returned by <see cref="CopySampleMod"/>.</param>
		/// <returns>The path.</returns>
		public static String SampleScript(String sourceFolder)
		{
			return Path.Combine(sourceFolder, "gl_g_ammobox.ssl");
		}

		/// <summary>
		/// Creates a small dialogue mod the way the Fallout 2 mods are laid out: <c>scripts\dlgtest.ssl</c> with <c>dlgtest.h</c>, the game's
		/// <c>scripts\scripts.lst</c> and the message files <c>text\english\dialog\dlgtest.msg</c> and <c>text\french\dialog\dlgtest.msg</c>. The script
		/// has <c>talk_p_proc</c>, nodes that link to each other, and deliberate weak points: an unreachable node, a dead end, a target that is not
		/// defined, and two messages the file lacks.
		/// </summary>
		/// <param name="root">The folder of the mod.</param>
		/// <returns>The paths of the files.</returns>
		public static DialogueMod CreateDialogueMod(String root)
		{
			String scripts = Path.Combine(root, "scripts");
			String english = Path.Combine(root, "text", "english", "dialog");
			String french = Path.Combine(root, "text", "french", "dialog");
			Directory.CreateDirectory(scripts);
			Directory.CreateDirectory(english);
			Directory.CreateDirectory(french);

			String header = Path.Combine(scripts, "dlgtest.h");
			String script = Path.Combine(scripts, DialogueScriptName);
			String list = Path.Combine(scripts, "scripts.lst");
			String englishMessages = Path.Combine(english, "dlgtest.msg");
			String frenchMessages = Path.Combine(french, "dlgtest.msg");
			File.WriteAllText(header, DialogueHeader);
			File.WriteAllText(script, DialogueScript);
			File.WriteAllText(list, "dlgtest.int       ; Dialogue test       # local_vars=0\r\n");
			File.WriteAllText(englishMessages, EnglishMessages);
			File.WriteAllText(frenchMessages, FrenchMessages);

			// What the registration of a script needs: scrname.msg of the game and the scripts.h of the mod.
			String headers = Path.Combine(root, "headers");
			String game = Path.Combine(root, "text", "english", "game");
			Directory.CreateDirectory(headers);
			Directory.CreateDirectory(game);
			String scriptsHeader = Path.Combine(headers, "scripts.h");
			String nameMessages = Path.Combine(game, "scrname.msg");
			File.WriteAllText(scriptsHeader, "#define SCRIPT_DLGTEST          (1)\r\n");
			File.WriteAllText(nameMessages, "{101}{}{Dialogue test}\r\n");
			String ugly = Path.Combine(scripts, "ugly.ssl");
			File.WriteAllText(ugly, UglyScript);
			return new DialogueMod(root, scripts, script, header, list, englishMessages, frenchMessages, headers, scriptsHeader, nameMessages, ugly);
		}

		/// <summary>Creates a script with deliberate semantic mistakes: calls with a wrong number of arguments and an unknown name.</summary>
		/// <param name="folder">The folder.</param>
		/// <returns>The path of the script.</returns>
		public static String CreateScriptWithMistakes(String folder)
		{
			Directory.CreateDirectory(folder);
			String path = Path.Combine(folder, "mistakes.ssl");
			File.WriteAllText(path, MistakesScript);
			return path;
		}

		/// <summary>Creates a script that calls a procedure before it is declared (the compiler reads the file once): the diagnostics offer to declare it.</summary>
		/// <param name="folder">The folder.</param>
		/// <returns>The path of the script.</returns>
		public static String CreateScriptWithMissingDeclaration(String folder)
		{
			Directory.CreateDirectory(folder);
			String path = Path.Combine(folder, "quickfix.ssl");
			File.WriteAllText(path, MissingDeclarationScript);
			return path;
		}

		/// <summary>Creates a plain script with a procedure, a loop and a few lines the editor tests type after.</summary>
		/// <param name="folder">The folder.</param>
		/// <returns>The path of the script.</returns>
		public static String CreateEditableScript(String folder)
		{
			Directory.CreateDirectory(folder);
			String path = Path.Combine(folder, "editme.ssl");
			File.WriteAllText(path, EditableScript);
			return path;
		}

		/// <summary>Creates the content of <c>settings.json</c> for a mod folder: the folder is the project, the scripts folder is the output folder.</summary>
		/// <param name="mod">The mod.</param>
		/// <returns>The JSON text.</returns>
		public static String CreateSettingsFor(DialogueMod mod)
		{
			Dictionary<String, Object> settings = new()
			{
				["isFirstRunCompleted"] = true,
				["paths"] = new Dictionary<String, Object?>
				{
					["outputDirectory"] = mod.Scripts,
					["headersDirectory"] = mod.Headers,
					["scriptsHeaderFile"] = mod.ScriptsHeader,
					["solutionFolder"] = mod.Root,
				},
				["messages"] = new Dictionary<String, Object?>
				{
					["associateByScriptId"] = true,
					["language"] = "english",
				},
			};
			return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
		}

		/// <summary>Creates the content of <c>settings.json</c> for a project that is only a folder of scripts.</summary>
		/// <param name="solutionFolder">The folder of the project.</param>
		/// <returns>The JSON text.</returns>
		public static String CreateSettingsForFolder(String solutionFolder)
		{
			Dictionary<String, Object> settings = new()
			{
				["isFirstRunCompleted"] = true,
				["paths"] = new Dictionary<String, Object?> { ["solutionFolder"] = solutionFolder },
			};
			return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
		}

		private static void CopyFolder(String source, String destination)
		{
			foreach (String directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
			}

			Directory.CreateDirectory(destination);
			foreach (String file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
			{
				File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), true);
			}
		}

		private const String DialogueHeader =
			"/* The macros of the dialogue test mod, written like the command.h of the Fallout 2 mods. */\r\n"
			+ "#define SCRIPT_DLGTEST 1\r\n"
			+ "#define NAME SCRIPT_DLGTEST\r\n"
			+ "#define mstr(x) message_str(NAME, x)\r\n"
			+ "#define Reply(x) gsay_reply(NAME, x)\r\n"
			+ "#define NMessage(x) gsay_message(NAME, x, 50)\r\n"
			+ "#define NOption(m, n, i) giq_option(i, NAME, m, n, 50)\r\n"
			+ "#define NLowOption(m, n) giq_option(-3, NAME, m, n, 50)\r\n"
			+ "#define gSay_Start gsay_start\r\n"
			+ "#define gSay_End gsay_end\r\n";

		private const String DialogueScript =
			"/* A small dialogue for the UI smoke tests. */\r\n"
			+ "#include \"dlgtest.h\"\r\n"
			+ "\r\n"
			+ "procedure start;\r\n"
			+ "procedure talk_p_proc;\r\n"
			+ "procedure Node001;\r\n"
			+ "procedure Node002;\r\n"
			+ "procedure Node003;\r\n"
			+ "procedure Node004;\r\n"
			+ "procedure Node005;\r\n"
			+ "procedure Node999;\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure talk_p_proc begin\r\n"
			+ "\tstart_gdialog(NAME, self_obj, 4, -1, -1);\r\n"
			+ "\tgSay_Start;\r\n"
			+ "\t\tcall Node001;\r\n"
			+ "\tgSay_End;\r\n"
			+ "\tend_dialogue;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure Node001 begin\r\n"
			+ "\tReply(100);\r\n"
			+ "\tNOption(101, Node002, 4);\r\n"
			+ "\tNOption(102, Node003, 4);\r\n"
			+ "\tNOption(103, Node999, 4);\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure Node002 begin\r\n"
			+ "\tReply(110);\r\n"
			+ "\tNOption(111, Node004, 4);\r\n"
			+ "\tNOption(112, Node001, 4);\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure Node003 begin\r\n"
			+ "\tReply(120);\r\n"
			+ "\tNOption(121, Node777, 4);\r\n"
			+ "\tNOption(122, Node999, 4);\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure Node004 begin\r\n"
			+ "\tReply(130);\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure Node005 begin\r\n"
			+ "\tReply(140);\r\n"
			+ "\tNOption(141, Node999, 4);\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure Node999 begin\r\n"
			+ "end\r\n";

		private const String EnglishMessages =
			"# The messages of the dialogue test mod.\r\n"
			+ "{100}{}{Hello, stranger. What brings you to the wasteland?}\r\n"
			+ "{101}{}{Tell me more.}\r\n"
			+ "{102}{}{What happened here?}\r\n"
			+ "{103}{}{Goodbye.}\r\n"
			+ "{110}{}{It is a long story. Do you have time for it?}\r\n"
			+ "{111}{}{Go on, I am listening.}\r\n"
			+ "{112}{}{Not now.}\r\n"
			+ "{120}{}{The caravan never arrived. Nobody knows why.}\r\n"
			+ "{121}{}{Where did it go?}\r\n"
			+ "{122}{}{I have to leave.}\r\n"
			+ "{130}{}{And that is all there is to tell.}\r\n"
			+ "{500}{}{A message nobody uses.}\r\n";

		private const String FrenchMessages =
			"# Les messages du mod de test.\r\n"
			+ "{100}{}{Bonjour, étranger. Qu'est-ce qui vous amène dans les terres dévastées ?}\r\n"
			+ "{101}{}{Dites-m'en plus.}\r\n"
			+ "{102}{}{}\r\n"
			+ "{103}{}{Au revoir.}\r\n"
			+ "{110}{}{C'est une longue histoire. Avez-vous le temps ?}\r\n"
			+ "{111}{}{Continuez, je vous écoute.}\r\n"
			+ "{112}{}{Pas maintenant.}\r\n"
			+ "{120}{}{La caravane n'est jamais arrivée. Personne ne sait pourquoi.}\r\n"
			+ "{121}{}{Où est-elle allée ?}\r\n"
			+ "{122}{}{Je dois partir.}\r\n";

		private const String MistakesScript =
			"/* A script with deliberate mistakes for the UI smoke tests. */\r\n"
			+ "procedure start;\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "\tvariable roll;\r\n"
			+ "\troll := random(1);\r\n"
			+ "\tdisplay_msg(\"one\", \"two\");\r\n"
			+ "\tunknown_function_name(roll);\r\n"
			+ "end\r\n";

		private const String UglyScript =
			"procedure start;\r\n"
			+ "procedure start begin\r\n"
			+ "variable a;\r\n"
			+ "a:=1+2;\r\n"
			+ "if(a==3)then begin\r\n"
			+ "display_msg(\"three\");\r\n"
			+ "end\r\n"
			+ "end\r\n";

		private const String MissingDeclarationScript =
			"/* A script that calls a procedure before it is declared. */\r\n"
			+ "procedure start;\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "\tcall helper_proc;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure helper_proc begin\r\n"
			+ "end\r\n";

		private const String EditableScript =
			"/* A script for the UI smoke tests that type into the editor. */\r\n"
			+ "procedure start;\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "\r\n"
			+ "end\r\n";
	}
}