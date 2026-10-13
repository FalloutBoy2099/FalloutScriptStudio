namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// The identifiers of the options a project can override (the names of the properties of <see cref="ProjectSettings"/>). They tie a
	/// control of the Settings window to the same option in the project settings window.
	/// </summary>
	public static class ProjectOptions
	{
		/// <summary>The folder with the header files.</summary>
		public const String HeadersDirectory = nameof(ProjectSettings.HeadersDirectory);

		/// <summary>The path of <c>scripts.h</c>.</summary>
		public const String ScriptsHeaderFile = nameof(ProjectSettings.ScriptsHeaderFile);

		/// <summary>The folder for compiled scripts.</summary>
		public const String OutputDirectory = nameof(ProjectSettings.OutputDirectory);

		/// <summary>Compile next to the source file.</summary>
		public const String CompileToSourceDirectory = nameof(ProjectSettings.CompileToSourceDirectory);

		/// <summary>Search the includes in the headers folder.</summary>
		public const String SearchIncludePath = nameof(ProjectSettings.SearchIncludePath);

		/// <summary>The preprocessor.</summary>
		public const String Preprocessor = nameof(ProjectSettings.Preprocessor);

		/// <summary>The selected preprocessor definition.</summary>
		public const String PreprocessorDefine = nameof(ProjectSettings.PreprocessorDefine);

		/// <summary>The definitions the project adds to the toolbar list.</summary>
		public const String Defines = nameof(ProjectSettings.Defines);

		/// <summary>The optimization level.</summary>
		public const String Optimization = nameof(ProjectSettings.Optimization);

		/// <summary>Short-circuit evaluation.</summary>
		public const String ShortCircuit = nameof(ProjectSettings.ShortCircuit);

		/// <summary>Backward compatibility mode.</summary>
		public const String BackwardCompatibility = nameof(ProjectSettings.BackwardCompatibility);

		/// <summary>Show compiler warnings.</summary>
		public const String ShowWarnings = nameof(ProjectSettings.ShowWarnings);

		/// <summary>Produce debug information.</summary>
		public const String DebugInfo = nameof(ProjectSettings.DebugInfo);

		/// <summary>The folders searched for message files.</summary>
		public const String MessageSearchDirectories = nameof(ProjectSettings.MessageSearchDirectories);

		/// <summary>The path of <c>scripts.lst</c>.</summary>
		public const String ScriptsListFile = nameof(ProjectSettings.ScriptsListFile);
	}
}
