namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Text;

	/// <summary>The generated project of the global variable tests and performance tests.</summary>
	public sealed partial class PerformanceInputs
	{
		/// <summary>The number of <c>GVAR_*</c> macros of the generated project.</summary>
		public const Int32 GlobalMacroCount = 2000;

		/// <summary>
		/// Gets the root of a project of <see cref="ProjectFileCount"/> files that read and write global variables (created on first use):
		/// a header with <see cref="GlobalMacroCount"/> <c>GVAR_*</c> macros and scripts that use them, sfall globals by name and by number,
		/// map and local variables, and a few mistakes (a variable that is only read, a name that is not 8 characters long).
		/// </summary>
		public String GlobalsProjectFolder
		{
			get
			{
				String root = Path.Combine(this.Folder, "globals-project");
				if (!Directory.Exists(root))
				{
					BuildGlobalsProject(root, ProjectFileCount);
				}

				return root;
			}
		}

		/// <summary>Creates a project that uses global variables.</summary>
		/// <param name="root">The folder to create.</param>
		/// <param name="files">The number of scripts.</param>
		public static void BuildGlobalsProject(String root, Int32 files)
		{
			Random random = new Random(4242);
			Directory.CreateDirectory(Path.Combine(root, "headers"));
			StringBuilder header = new StringBuilder(GlobalMacroCount * 28);
			for (Int32 g = 0; g < GlobalMacroCount; g++)
			{
				header.Append("#define GVAR_QUEST_").Append(g.ToString("D4", CultureInfo.InvariantCulture)).Append(" (").Append(g + 100).Append(")\r\n");
			}

			header.Append("#define SGNAME_COMMON \"commonvr\"\r\n");
			WriteOld(Path.Combine(root, "headers", "gvars.h"), header.ToString());
			for (Int32 i = 0; i < files; i++)
			{
				String directory = Path.Combine(root, "scripts", "group" + (i % 50).ToString("D2", CultureInfo.InvariantCulture));
				Directory.CreateDirectory(directory);
				StringBuilder text = new StringBuilder(1024);
				text.Append("#include \"..\\..\\headers\\gvars.h\"\r\n#define LVAR_Seen (0)\r\nvariable total;\r\n\r\n");
				Int32 procedures = 2 + random.Next(4);
				for (Int32 p = 0; p < procedures; p++)
				{
					text.Append("procedure proc_").Append(p).Append(" begin\r\n\tvariable index;\r\n");
					Int32 statements = 2 + random.Next(5);
					for (Int32 s = 0; s < statements; s++)
					{
						String macro = "GVAR_QUEST_" + random.Next(GlobalMacroCount).ToString("D4", CultureInfo.InvariantCulture);
						switch (random.Next(8))
						{
							case 0:
								text.Append("\tset_global_var(").Append(macro).Append(", ").Append(random.Next(5)).Append(");\r\n");
								break;
							case 1:
								text.Append("\ttotal := global_var(").Append(macro).Append(");\r\n");
								break;
							case 2:
								text.Append("\tinc_global_var(").Append(macro).Append(");\r\n");
								break;
							case 3:
								text.Append("\tset_sfall_global(SGNAME_COMMON, ").Append(random.Next(100)).Append(");\r\n");
								break;
							case 4:
								text.Append("\ttotal := get_sfall_global_int(\"flag").Append(random.Next(1000).ToString("D5", CultureInfo.InvariantCulture)).Append("\");\r\n");
								break;
							case 5:
								text.Append("\tset_local_var(LVAR_Seen, 1);\r\n\ttotal := local_var(LVAR_Seen);\r\n");
								break;
							case 6:
								text.Append("\tset_map_var(").Append(random.Next(30)).Append(", index);\r\n");
								break;
							default:
								text.Append("\ttotal := global_var(").Append(random.Next(GlobalMacroCount)).Append(" + index);\r\n\tdisplay_msg(\"set_global_var(1, 2) is only text\");\r\n");
								break;
						}
					}

					text.Append("end\r\n\r\n");
				}

				WriteOld(Path.Combine(directory, "script_" + i.ToString("D5", CultureInfo.InvariantCulture) + ".ssl"), text.ToString());
			}
		}
	}
}