namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Locates <c>scripts.lst</c>, <c>scrname.msg</c> and <c>scripts.h</c> from the configured folders.
	/// </summary>
	public static class ScriptRegistryPathResolver
	{
		/// <summary>The name of the list of registered scripts.</summary>
		public const String ListFileName = "scripts.lst";

		/// <summary>The name of the message file with the in-game names of the scripts.</summary>
		public const String NameMessageFileName = "scrname.msg";

		/// <summary>
		/// Locates the files.
		/// </summary>
		/// <param name="outputDirectory">The game scripts folder.</param>
		/// <param name="scriptsHeaderFile">The configured <c>scripts.h</c>.</param>
		/// <param name="language">The game language, the folder below <c>text</c>.</param>
		/// <param name="scriptsListFile">The path of <c>scripts.lst</c> when the project names it; otherwise it is looked for in the output folder.</param>
		/// <returns>The paths (when the list exists) and the problems.</returns>
		public static ScriptRegistryPathResult Resolve(String? outputDirectory, String? scriptsHeaderFile, String language, String? scriptsListFile = null)
		{
			List<ScriptRegistryProblem> problems = new List<ScriptRegistryProblem>();
			String? scriptsDirectory = String.IsNullOrWhiteSpace(scriptsListFile) ? outputDirectory : Path.GetDirectoryName(scriptsListFile);
			if (String.IsNullOrEmpty(scriptsDirectory))
			{
				problems.Add(new ScriptRegistryProblem(ScriptRegistryProblemKind.OutputDirectoryNotSet, true, Loc.Get("Str.ScriptTools.RegPathOutputNotSet")));
				return new ScriptRegistryPathResult(null, problems);
			}

			String listPath = String.IsNullOrWhiteSpace(scriptsListFile) ? Path.Combine(scriptsDirectory, ListFileName) : scriptsListFile;
			if (!File.Exists(listPath))
			{
				problems.Add(new ScriptRegistryProblem(ScriptRegistryProblemKind.ScriptsListMissing, true, Loc.Format("Str.ScriptTools.RegPathListMissing", ListFileName)));
				return new ScriptRegistryPathResult(null, problems);
			}

			String? messagePath = Path.GetFullPath(Path.Combine(scriptsDirectory, "..", "text", String.IsNullOrEmpty(language) ? "english" : language, "game", NameMessageFileName));
			if (!File.Exists(messagePath))
			{
				problems.Add(new ScriptRegistryProblem(ScriptRegistryProblemKind.NameMessageMissing, false, Loc.Format("Str.ScriptTools.RegPathFileNotFound", NameMessageFileName)));
				messagePath = null;
			}

			String? headerPath = null;
			if (String.IsNullOrEmpty(scriptsHeaderFile))
			{
				problems.Add(new ScriptRegistryProblem(ScriptRegistryProblemKind.HeaderNotSet, false, Loc.Get("Str.ScriptTools.RegPathHeaderNotSet")));
			}
			else if (File.Exists(scriptsHeaderFile))
			{
				headerPath = scriptsHeaderFile;
			}
			else
			{
				problems.Add(new ScriptRegistryProblem(ScriptRegistryProblemKind.HeaderMissing, false, Loc.Format("Str.ScriptTools.RegPathFileNotFound", scriptsHeaderFile)));
			}

			return new ScriptRegistryPathResult(new ScriptRegistryPaths(listPath, messagePath, headerPath), problems);
		}
	}
}
