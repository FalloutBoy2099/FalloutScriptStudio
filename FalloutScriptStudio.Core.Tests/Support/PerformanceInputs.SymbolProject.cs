namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Text;

	/// <summary>The generated project of the symbol index tests and performance tests.</summary>
	public sealed partial class PerformanceInputs
	{
		/// <summary>
		/// Gets the root of a project of <see cref="ProjectFileCount"/> files for the project symbol index (created on first use): scripts
		/// with forward declarations, procedures, macros and variables that include a shared header, headers with macros, and noise
		/// (message files, binary <c>.int</c> files). Procedure names repeat in several scripts, as they do in a real mod.
		/// </summary>
		public String SymbolProjectFolder
		{
			get
			{
				String root = Path.Combine(this.Folder, "symbol-project");
				if (!Directory.Exists(root))
				{
					BuildSymbolProject(root, ProjectFileCount);
				}

				return root;
			}
		}

		/// <summary>
		/// Creates a project for the symbol index: <paramref name="files"/> files in sub folders, 70 % scripts that each have 2 to 9
		/// procedures (with forward declarations), a few macros and variables and an include of the shared header; 15 % headers with 5 to
		/// 60 macros and a variable; 10 % message files and 5 % binary <c>.int</c> files, which the index ignores.
		/// </summary>
		/// <param name="root">The folder to create.</param>
		/// <param name="files">The number of files.</param>
		public static void BuildSymbolProject(String root, Int32 files)
		{
			Random random = new Random(777);
			Directory.CreateDirectory(Path.Combine(root, "headers"));
			WriteOld(
				Path.Combine(root, "headers", "shared.h"),
				"#define SHARED_LIMIT 10\r\n#define shared_max(a, b) (((a) > (b)) ? (a) : (b))\r\nvariable shared_counter;\r\n");
			for (Int32 i = 0; i < files; i++)
			{
				String directory = Path.Combine(root, "scripts", "group" + (i % 50).ToString("D2", CultureInfo.InvariantCulture), "set" + ((i / 50) % 10).ToString(CultureInfo.InvariantCulture));
				Directory.CreateDirectory(directory);
				Int32 kind = i % 20;
				String id = i.ToString("D5", CultureInfo.InvariantCulture);
				if (kind >= 19)
				{
					Byte[] bytes = new Byte[800];
					random.NextBytes(bytes);
					bytes[7] = 0;
					File.WriteAllBytes(Path.Combine(directory, "file_" + id + ".int"), bytes);
					continue;
				}

				if (kind >= 17)
				{
					File.WriteAllText(Path.Combine(directory, "file_" + id + ".msg"), "{100}{}{Hello " + id + "}\r\n");
					continue;
				}

				StringBuilder text = new StringBuilder(2048);
				if (kind >= 14)
				{
					text.Append("#ifndef HDR_").Append(id).Append("_H\r\n#define HDR_").Append(id).Append("_H\r\n");
					Int32 macros = 5 + random.Next(56);
					for (Int32 m = 0; m < macros; m++)
					{
						text.Append("#define HM").Append(id).Append('_').Append(m).Append(m % 3 == 0 ? "(x) ((x) + " + m.ToString(CultureInfo.InvariantCulture) + ")" : " " + m.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
					}

					text.Append("variable hv_").Append(id).Append(";\r\n#endif\r\n");
					WriteOld(Path.Combine(directory, "hdr_" + id + ".h"), text.ToString());
					continue;
				}

				text.Append("#include \"..\\..\\..\\headers\\shared.h\"\r\n");
				text.Append("#define SCRIPT_ID ").Append(i).Append("\r\n#define local_scale_").Append(id).Append("(v) ((v) * 2)\r\nvariable counter_").Append(id).Append(", total := 0;\r\n\r\n");
				Int32 procedures = 2 + random.Next(8);
				for (Int32 p = 0; p < procedures; p++)
				{
					text.Append("procedure ").Append(ProcedureName(p, id)).Append(p % 2 == 0 ? ";" : "(variable a);").Append("\r\n");
				}

				text.Append("\r\n");
				for (Int32 p = 0; p < procedures; p++)
				{
					String name = ProcedureName(p, id);
					text.Append("// Procedure ").Append(name).Append("\r\nprocedure ").Append(name).Append(p % 2 == 0 ? String.Empty : "(variable a)").Append(" begin\r\n");
					text.Append("\tvariable i, unused_").Append(p).Append(";\r\n");
					Int32 statements = 3 + random.Next(12);
					for (Int32 s = 0; s < statements; s++)
					{
						text.Append("\ti = i + ").Append(s).Append(";\r\n\tdisplay_msg(\"Statement ").Append(s).Append("\");\r\n");
					}

					text.Append("end\r\n\r\n");
				}

				WriteOld(Path.Combine(directory, "file_" + id + ".ssl"), text.ToString());
			}
		}

		/// <summary>Writes a file and gives it an old time stamp, as a file that was saved long ago (the index re-reads a file written less than 2 seconds earlier).</summary>
		private static void WriteOld(String path, String text)
		{
			File.WriteAllText(path, text);
			File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		}

		private static String ProcedureName(Int32 index, String id)
		{
			return index == 0 ? "start" : "proc_" + (index % 4).ToString(CultureInfo.InvariantCulture) + "_" + id;
		}
	}
}
