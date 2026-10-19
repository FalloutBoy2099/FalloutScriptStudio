namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Large scripts that are <b>valid</b> SSL, for the tests of the semantic diagnostics: the generated scripts of the other performance
	/// tests are built from fragments of procedures and use names that nothing declares, so they cannot show that the analysis is free of
	/// false positives. These are made of whole real scripts (the <c>gl_g_*.ssl</c> files of the FO2tweaksMk2 sample) copied many times, every
	/// copy with its own procedure and global variable names.
	/// </summary>
	public sealed partial class PerformanceInputs
	{
		private static readonly Regex _globalName = new Regex(@"^(?:procedure|variable)[ \t]+(?<name>[A-Za-z_]\w*)", RegexOptions.Multiline | RegexOptions.CultureInvariant);

		/// <summary>Gets the path of a valid script of about 5 000 lines made of copies of real scripts.</summary>
		public String ValidScript5kPath => this.GetOrCreate("scripts\\valid_script_5000.ssl", () => BuildValidScript(5000));

		/// <summary>Gets the path of a valid script of about 20 000 lines made of copies of real scripts.</summary>
		public String ValidScript20kPath => this.GetOrCreate("scripts\\valid_script_20000.ssl", () => BuildValidScript(20000));

		/// <summary>Gets the text of the valid script of about 5 000 lines.</summary>
		public String ValidScript5kText => File.ReadAllText(this.ValidScript5kPath);

		/// <summary>Gets the text of the valid script of about 20 000 lines.</summary>
		public String ValidScript20kText => File.ReadAllText(this.ValidScript20kPath);

		/// <summary>Builds a script by copying the real sample scripts until it has the requested number of lines.</summary>
		/// <param name="lines">The approximate number of lines.</param>
		/// <returns>The text; empty when the samples are not there.</returns>
		public static String BuildValidScript(Int32 lines)
		{
			String folder = Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");
			if (!Directory.Exists(folder))
			{
				return String.Empty;
			}

			List<String> sources = Directory.GetFiles(folder, "gl_g_*.ssl").OrderBy(f => f, StringComparer.Ordinal).Select(File.ReadAllText).ToList();
			StringBuilder text = new StringBuilder(lines * 48);
			Int32 written = 0;
			Int32 instance = 0;
			for (Int32 copy = 0; written < lines && sources.Count > 0; copy++)
			{
				foreach (String source in sources)
				{
					String renamed = Rename(source, instance++);
					text.Append(renamed);
					if (!renamed.EndsWith('\n'))
					{
						text.Append("\r\n");
					}

					written += renamed.Count(c => c == '\n');
					if (written >= lines)
					{
						break;
					}
				}
			}

			return text.ToString();
		}

		private static String Rename(String source, Int32 instance)
		{
			String suffix = "_s" + instance.ToString(CultureInfo.InvariantCulture);
			String result = source;
			foreach (String name in _globalName.Matches(source).Select(m => m.Groups["name"].Value).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				result = Regex.Replace(result, @"\b" + Regex.Escape(name) + @"\b", name + suffix, RegexOptions.CultureInvariant);
			}

			return result;
		}
	}
}