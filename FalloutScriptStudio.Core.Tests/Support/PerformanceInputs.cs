namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Generates the large, realistic inputs of the <c>Performance</c> tests under <c>_test_files\work</c>: SSL scripts of 5 000 and 20 000
	/// lines built from the procedures of the real FO2tweaksMk2 sample, a header with 2 000 macros, a 30 000 line <c>.msg</c> file, a dialogue
	/// script with 200 nodes and a project folder with 5 000 files. The content is deterministic (fixed seed), so the numbers of two runs
	/// compare. Each file is generated on first use. This file is linked into both test projects.
	/// </summary>
	public sealed partial class PerformanceInputs : IDisposable
	{
		/// <summary>The number of macros of the generated header.</summary>
		public const Int32 HeaderMacroCount = 2000;

		/// <summary>The macro that a generated script uses <see cref="HotMacroUses"/> times.</summary>
		public const String HotMacro = "PERF_HOT";

		/// <summary>How often a generated script uses <see cref="HotMacro"/>.</summary>
		public const Int32 HotMacroUses = 2000;

		/// <summary>The number of nodes of the generated dialogue script (without the start and the exit node).</summary>
		public const Int32 DialogueNodeCount = 200;

		/// <summary>The number of files of the generated project folder.</summary>
		public const Int32 ProjectFileCount = 5000;

		private static readonly Regex _procedureHeader = new Regex(@"^procedure\s+(\w+)\s*(\([^)]*\))?\s*begin\s*$", RegexOptions.CultureInvariant);

		private readonly TestFolder _folder = new TestFolder("perf-inputs");
		private readonly Dictionary<String, String> _files = new(StringComparer.OrdinalIgnoreCase);
		private List<SampleProcedure>? _pool;

		/// <summary>Gets the folder that holds every generated file.</summary>
		public String Folder => this._folder.Path;

		/// <summary>Gets the full path of the generated header with <see cref="HeaderMacroCount"/> macros.</summary>
		public String HeaderPath => this.GetOrCreate("headers\\perf_macros.h", () => BuildHeader(HeaderMacroCount));

		/// <summary>Gets the text of the generated header.</summary>
		public String HeaderText => File.ReadAllText(this.HeaderPath);

		/// <summary>
		/// Gets the full path of a generated SSL script (it includes <see cref="HeaderPath"/>).
		/// </summary>
		/// <param name="lines">The approximate number of lines.</param>
		/// <param name="procedures">The number of procedures.</param>
		/// <returns>The path.</returns>
		public String GetScriptPath(Int32 lines, Int32 procedures)
		{
			_ = this.HeaderPath;
			return this.GetOrCreate("scripts\\perf_script_" + lines.ToString(CultureInfo.InvariantCulture) + ".ssl", () => this.BuildScript(lines, procedures));
		}

		/// <summary>Gets the path of the script of about 5 000 lines and 75 procedures.</summary>
		public String Script5kPath => this.GetScriptPath(5000, 75);

		/// <summary>Gets the path of the script of about 20 000 lines and 300 procedures.</summary>
		public String Script20kPath => this.GetScriptPath(20000, 300);

		/// <summary>Gets the text of the script of about 20 000 lines.</summary>
		public String Script20kText => File.ReadAllText(this.Script20kPath);

		/// <summary>Gets the text of the script of about 5 000 lines.</summary>
		public String Script5kText => File.ReadAllText(this.Script5kPath);

		/// <summary>Gets the path of the generated <c>.msg</c> file of 30 000 lines.</summary>
		public String MessagePath => this.GetOrCreate("text\\perf_messages.msg", () => BuildMessages(30000));

		/// <summary>Gets the path of the dialogue script with <see cref="DialogueNodeCount"/> nodes.</summary>
		public String DialogueScriptPath => this.GetOrCreate("dialogue\\perf_dialogue.ssl", () => BuildDialogueScript(DialogueNodeCount));

		/// <summary>Gets the path of the message file of the dialogue script.</summary>
		public String DialogueMessagePath => this.GetOrCreate("dialogue\\perf_dialogue.msg", () => BuildDialogueMessages(DialogueNodeCount));

		/// <summary>Gets the root of the project folder with <see cref="ProjectFileCount"/> files (created on first use).</summary>
		public String ProjectFolder
		{
			get
			{
				String root = Path.Combine(this._folder.Path, "project");
				if (!Directory.Exists(root))
				{
					BuildProjectFolder(root, ProjectFileCount);
				}

				return root;
			}
		}

		/// <summary>Deletes the generated files.</summary>
		public void Dispose()
		{
			this._folder.Dispose();
		}

		/// <summary>Builds the header: a mix of constants, function-like macros, multi-line macros and documented macros.</summary>
		/// <param name="count">The number of macros.</param>
		/// <returns>The text.</returns>
		public static String BuildHeader(Int32 count)
		{
			StringBuilder text = new StringBuilder(count * 48);
			text.Append("/* Generated header of the performance tests. */\r\n\r\n");
			for (Int32 i = 0; i < count; i++)
			{
				String id = i.ToString("D4", CultureInfo.InvariantCulture);
				switch (i % 4)
				{
					case 0:
						text.Append("#define PERF_CONST_").Append(id).Append(' ').Append((i * 7) % 997).Append("\r\n");
						break;
					case 1:
						text.Append("#define perf_func_").Append(id).Append("(a, b) ((a) + (b) * ").Append((i % 13) + 1).Append(")\r\n");
						break;
					case 2:
						text.Append("#define PERF_BLOCK_").Append(id).Append("(x) \\\r\n");
						text.Append("\tbegin \\\r\n");
						text.Append("\t\tdisplay_msg(x + ").Append(i).Append("); \\\r\n");
						text.Append("\tend\r\n");
						break;
					default:
						text.Append("/** Documented macro number ").Append(id).Append(" **/\r\n");
						text.Append("#define PERF_DOC_").Append(id).Append(" \"text ").Append(id).Append("\"\r\n");
						break;
				}
			}

			return text.ToString();
		}

		/// <summary>Builds a message file: comments, NPC messages, player lines (leading tab), multi-line messages and blank lines.</summary>
		/// <param name="lines">The number of physical lines.</param>
		/// <returns>The text.</returns>
		public static String BuildMessages(Int32 lines)
		{
			StringBuilder text = new StringBuilder(lines * 60);
			Int32 written = 0;
			Int32 id = 100;
			Int32 group = 0;
			while (written < lines)
			{
				group++;
				text.Append("# Node ").Append(group).Append(" - generated comment\r\n");
				text.Append('{').Append(id).Append("}{}{Hello traveler, this is the greeting number ").Append(group).Append(". Welcome to the wasteland.}\r\n");
				id++;
				text.Append("\t{").Append(id).Append("}{}{I need your help with ").Append(group).Append(", please.}\r\n");
				id++;
				text.Append("\t{").Append(id).Append("}{}{Goodbye.}\r\n");
				id++;
				text.Append('{').Append(id).Append("}{}{This is a long message that continues\r\n");
				text.Append("on the second physical line of the entry\r\n");
				text.Append("and ends on the third one.}\r\n");
				id++;
				text.Append("\r\n");
				text.Append('{').Append(id).Append("}{}{Final words of group ").Append(group).Append(".}\r\n");
				id += 5;
				written += 9;
			}

			return text.ToString();
		}

		/// <summary>Builds a dialogue script: <c>talk_p_proc</c>, <c>nodes</c> node procedures linked to each other and an exit node.</summary>
		/// <param name="nodes">The number of nodes.</param>
		/// <returns>The text.</returns>
		public static String BuildDialogueScript(Int32 nodes)
		{
			StringBuilder text = new StringBuilder(nodes * 400);
			text.Append("/* Generated dialogue script of the performance tests. */\r\n");
			text.Append("#define NAME SCRIPT_PERF\r\n#define SCRIPT_PERF 321\r\n#define mstr(x) message_str(NAME, x)\r\n\r\n");
			text.Append("procedure start;\r\nprocedure talk_p_proc;\r\n");
			for (Int32 n = 1; n <= nodes; n++)
			{
				text.Append("procedure Node").Append(n.ToString("D3", CultureInfo.InvariantCulture)).Append(";\r\n");
			}

			text.Append("procedure Node999;\r\n\r\nprocedure start begin\r\nend\r\n\r\n");
			text.Append("procedure talk_p_proc begin\r\n\tReply(100);\r\n\tNOption(101, Node001, 4);\r\n\tNOption(102, Node002, 4);\r\n\tNLowOption(103, Node999);\r\nend\r\n\r\n");
			for (Int32 n = 1; n <= nodes; n++)
			{
				Int32 baseMessage = 1000 + (n * 10);
				String name = "Node" + n.ToString("D3", CultureInfo.InvariantCulture);
				Int32 next1 = (n % nodes) + 1;
				Int32 next2 = ((n * 7) % nodes) + 1;
				text.Append("procedure ").Append(name).Append(" begin\r\n");
				text.Append("\tReply(").Append(baseMessage).Append(");\r\n");
				text.Append("\tNOption(").Append(baseMessage + 1).Append(", Node").Append(next1.ToString("D3", CultureInfo.InvariantCulture)).Append(", 4);\r\n");
				text.Append("\tif (global_var(").Append(n).Append(") == 1) then begin\r\n");
				text.Append("\t\tGOption(").Append(baseMessage + 2).Append(", Node").Append(next2.ToString("D3", CultureInfo.InvariantCulture)).Append(", 5);\r\n");
				text.Append("\tend else begin\r\n");
				text.Append("\t\tBOption(").Append(baseMessage + 3).Append(", Node999, -1);\r\n");
				text.Append("\tend\r\n");
				text.Append("\t// a comment with Reply(1) in it\r\n");
				text.Append("\tNLowOption(").Append(baseMessage + 4).Append(", Node999);\r\n");
				text.Append("\tgsay_message(NAME, mstr(").Append(baseMessage + 5).Append("), 50);\r\n");
				text.Append("end\r\n\r\n");
			}

			text.Append("procedure Node999 begin\r\nend\r\n");
			return text.ToString();
		}

		/// <summary>Builds the message file that matches <see cref="BuildDialogueScript"/>.</summary>
		/// <param name="nodes">The number of nodes.</param>
		/// <returns>The text.</returns>
		public static String BuildDialogueMessages(Int32 nodes)
		{
			StringBuilder text = new StringBuilder(nodes * 300);
			text.Append("{100}{}{Hello stranger.}\r\n\t{101}{}{Tell me more.}\r\n\t{102}{}{What happened here?}\r\n\t{103}{}{Goodbye.}\r\n");
			for (Int32 n = 1; n <= nodes; n++)
			{
				Int32 baseMessage = 1000 + (n * 10);
				text.Append("# Node ").Append(n).Append("\r\n");
				text.Append('{').Append(baseMessage).Append("}{}{The reply of the node ").Append(n).Append(" is a fairly long sentence for a wasteland dweller.}\r\n");
				for (Int32 k = 1; k <= 4; k++)
				{
					text.Append("\t{").Append(baseMessage + k).Append("}{}{Option ").Append(k).Append(" of node ").Append(n).Append(".}\r\n");
				}

				text.Append('{').Append(baseMessage + 5).Append("}{}{A spoken message of node ").Append(n).Append(".}\r\n");
			}

			return text.ToString();
		}

		/// <summary>
		/// Creates a project folder: <paramref name="files"/> files spread over sub-folders, mostly <c>.ssl</c> scripts of a few kilobytes
		/// with a few <c>.h</c>, <c>.msg</c>, <c>.txt</c> and binary <c>.int</c> files. Every file contains the word <c>NeedleWord</c>
		/// on one line out of 25, and the word <c>rare_token_zq</c> in one file out of 200.
		/// </summary>
		/// <param name="root">The folder to create.</param>
		/// <param name="files">The number of files.</param>
		public static void BuildProjectFolder(String root, Int32 files)
		{
			Random random = new Random(12345);
			Directory.CreateDirectory(root);
			for (Int32 i = 0; i < files; i++)
			{
				String directory = Path.Combine(root, "scripts", "group" + (i % 50).ToString("D2", CultureInfo.InvariantCulture), "set" + ((i / 50) % 10).ToString(CultureInfo.InvariantCulture));
				Directory.CreateDirectory(directory);
				Int32 kind = i % 20;
				String extension = kind switch
				{
					< 14 => ".ssl",
					< 16 => ".h",
					< 18 => ".msg",
					19 => ".int",
					_ => ".txt",
				};
				String path = Path.Combine(directory, "file_" + i.ToString("D5", CultureInfo.InvariantCulture) + extension);
				if (extension == ".int")
				{
					Byte[] bytes = new Byte[1500];
					random.NextBytes(bytes);
					bytes[10] = 0;
					File.WriteAllBytes(path, bytes);
					continue;
				}

				StringBuilder text = new StringBuilder(4096);
				Int32 lines = 40 + random.Next(80);
				for (Int32 line = 0; line < lines; line++)
				{
					if (extension == ".msg")
					{
						text.Append('{').Append(100 + line).Append("}{}{Message ").Append(line).Append(" of file ").Append(i).Append(".}\r\n");
					}
					else if (line % 25 == 3)
					{
						text.Append("\tvariable NeedleWord_").Append(line).Append(" = ").Append(i).Append(";\r\n");
					}
					else if (line % 7 == 0)
					{
						text.Append("\tdisplay_msg(\"Some text in file ").Append(i).Append(" line ").Append(line).Append("\");\r\n");
					}
					else
					{
						text.Append("\tset_global_var(").Append(line).Append(", get_global_var(").Append(line + 1).Append(") + ").Append(i % 17).Append(");\r\n");
					}
				}

				if (i % 200 == 0)
				{
					text.Append("// rare_token_zq\r\n");
				}

				File.WriteAllText(path, text.ToString());
			}
		}

		/// <summary>Gets the folder with 300 header files of 100 macros each (a typical headers folder of a big mod).</summary>
		public String HeaderFolder
		{
			get
			{
				String root = Path.Combine(this._folder.Path, "headers-many");
				if (!Directory.Exists(root))
				{
					for (Int32 file = 0; file < 300; file++)
					{
						String directory = Path.Combine(root, "group" + (file % 10).ToString(CultureInfo.InvariantCulture));
						Directory.CreateDirectory(directory);
						StringBuilder text = new StringBuilder();
						text.Append("#ifndef HDR_").Append(file).Append("_H\r\n#define HDR_").Append(file).Append("_H\r\n");
						if (file % 3 == 0 && file > 0)
						{
							text.Append("#include \"..\\group").Append((file - 1) % 10).Append("\\hdr_").Append((file - 1).ToString("D3", CultureInfo.InvariantCulture)).Append(".h\"\r\n");
						}

						for (Int32 m = 0; m < 100; m++)
						{
							text.Append("/** Macro ").Append(file).Append('_').Append(m).Append(" **/\r\n#define HDR").Append(file).Append("_M").Append(m).Append("(x) ((x) * ").Append(m).Append(")\r\n");
						}

						text.Append("#endif\r\n");
						File.WriteAllText(Path.Combine(directory, "hdr_" + file.ToString("D3", CultureInfo.InvariantCulture) + ".h"), text.ToString());
					}
				}

				return root;
			}
		}

		private static List<SampleProcedure> LoadPool()
		{
			String source = Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");
			List<SampleProcedure> pool = [];
			foreach (String file in Directory.GetFiles(source, "*.ssl"))
			{
				String[] lines = File.ReadAllLines(file);
				for (Int32 i = 0; i < lines.Length; i++)
				{
					Match match = _procedureHeader.Match(lines[i]);
					if (!match.Success)
					{
						continue;
					}

					List<String> body = [];
					Int32 j = i + 1;
					while (j < lines.Length && !String.Equals(lines[j], "end", StringComparison.Ordinal))
					{
						body.Add(lines[j]);
						j++;
					}

					if (j < lines.Length && body.Count > 0)
					{
						pool.Add(new SampleProcedure(match.Groups[1].Value, body));
					}

					i = j;
				}
			}

			if (pool.Count == 0)
			{
				throw new InvalidOperationException("The sample scripts of FO2tweaksMk2 were not found in " + source);
			}

			return pool;
		}

		private String GetOrCreate(String relativePath, Func<String> build)
		{
			if (this._files.TryGetValue(relativePath, out String? existing))
			{
				return existing;
			}

			String path = this._folder.Write(relativePath, build());
			this._files[relativePath] = path;
			return path;
		}

		/// <summary>Builds a script: header include, macros, declarations, variables and procedures made of real statements of the sample.</summary>
		private String BuildScript(Int32 lines, Int32 procedures)
		{
			this._pool ??= LoadPool();
			Random random = new Random(lines);
			StringBuilder text = new StringBuilder(lines * 48);
			text.Append("/* Generated script of the performance tests (").Append(lines).Append(" lines). */\r\n");
			text.Append("#include \"..\\headers\\perf_macros.h\"\r\n");
			text.Append("#define SCRIPT_REALNAME \"perf_script\"\r\n");
			text.Append("#define ").Append(HotMacro).Append(" 42\r\n");
			for (Int32 i = 0; i < 60; i++)
			{
				text.Append("#define LOCAL_MACRO_").Append(i).Append("(x) ((x) + ").Append(i).Append(")\r\n");
			}

			text.Append("\r\n");
			for (Int32 p = 0; p < procedures; p++)
			{
				text.Append("procedure perf_proc_").Append(p.ToString("D3", CultureInfo.InvariantCulture)).Append("(variable arg1, variable arg2);\r\n");
			}

			text.Append("\r\nvariable begin\r\n");
			for (Int32 v = 0; v < 150; v++)
			{
				text.Append("\tglobal_").Append(v).Append(" = ").Append(v).Append(";\r\n");
			}

			text.Append("end\r\n");
			for (Int32 v = 0; v < 150; v++)
			{
				text.Append("variable extra_").Append(v).Append(" = ").Append(v).Append(";\r\n");
			}

			text.Append("\r\n");

			Int32 fixedLines = CountLines(text);
			Int32 perProcedure = Math.Max(8, (Int32)(((lines - fixedLines) / (Double)procedures) * 0.84));
			Int32 hotEvery = Math.Max(1, (lines - fixedLines) / HotMacroUses);
			Int32 hotUses = 0;
			Int32 sinceHot = 0;
			for (Int32 p = 0; p < procedures; p++)
			{
				text.Append("procedure perf_proc_").Append(p.ToString("D3", CultureInfo.InvariantCulture)).Append("(variable arg1, variable arg2) begin\r\n");
				Int32 written = 2;
				while (written < perProcedure)
				{
					SampleProcedure sample = this._pool[random.Next(this._pool.Count)];
					foreach (String line in sample.Body)
					{
						text.Append(line).Append("\r\n");
						written++;
					}

					text.Append("\t// section ").Append(p).Append('.').Append(written).Append(" uses PERF_CONST_").Append((random.Next(HeaderMacroCount / 4) * 4).ToString("D4", CultureInfo.InvariantCulture)).Append("\r\n");
					text.Append("\tdisplay_msg(\"quoted \\\"text\\\" of ").Append(p).Append("\" + PERF_DOC_").Append(((random.Next(HeaderMacroCount / 4) * 4) + 3).ToString("D4", CultureInfo.InvariantCulture)).Append(");\r\n");
					written += 2;
					if (written % 5 == 0)
					{
						text.Append("\t/* a block comment that\r\n\t   spans two lines */\r\n");
						written += 2;
					}

					sinceHot += sample.Body.Count + 4;
					while (sinceHot >= hotEvery && hotUses < HotMacroUses)
					{
						text.Append("\targ1 = arg2 + ").Append(HotMacro).Append(" * ").Append(hotUses % 9).Append(";\r\n");
						hotUses++;
						sinceHot -= hotEvery;
						written++;
					}
				}

				text.Append("end\r\n\r\n");
			}

			if (hotUses < HotMacroUses)
			{
				text.Append("procedure perf_tail begin\r\n");
				while (hotUses < HotMacroUses)
				{
					text.Append("\targ1 = arg2 + ").Append(HotMacro).Append(";\r\n");
					hotUses++;
				}

				text.Append("end\r\n");
			}

			return text.ToString();
		}

		private static Int32 CountLines(StringBuilder text)
		{
			Int32 count = 0;
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n')
				{
					count++;
				}
			}

			return count;
		}

		private sealed record SampleProcedure(String Name, IReadOnlyList<String> Body);
	}
}