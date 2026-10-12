namespace FalloutScriptStudio.Problems
{
	using System;

	/// <summary>The names of the producers of problems. A producer replaces or removes its own problems when its situation changes.</summary>
	public static class ProblemOrigins
	{
		/// <summary>Missing or unstartable compiler, preprocessor, batch file or decompiler: cleared when the next build starts.</summary>
		public const String BuildTool = "build-tool";

		/// <summary>The scripts that failed in the last mass compilation: cleared when the next one starts.</summary>
		public const String MassCompile = "mass-compile";

		/// <summary>The state of the native parser: cleared as soon as it works (or is turned off) again.</summary>
		public const String NativeParser = "native-parser";

		/// <summary>A document that could not be saved: cleared when it is saved.</summary>
		public const String Save = "save";

		/// <summary>The encoding a file was read with: cleared when the document is closed.</summary>
		public const String Encoding = "encoding";

		/// <summary>The problems of the settings files.</summary>
		public const String Settings = "settings";

		/// <summary>The offer to switch to the project of a script that was opened: cleared when the project changes.</summary>
		public const String ProjectDetection = "project-detection";
	}
}
