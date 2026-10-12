namespace FalloutScriptStudio.Workspace
{
	using System;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Builds the immutable parser options from the user settings.</summary>
	public static class ParserOptionsFactory
	{
		/// <summary>The file name of the native parser inside the resources folder.</summary>
		public const String NativeParserFileName = "parser.dll";

		/// <summary>Creates the parser options for the current settings.</summary>
		public static ParserOptions Create(ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			EffectiveSettings effective = settings.Effective;
			AppSettings current = effective.Settings;
			AppPaths paths = settings.Paths;
			return new ParserOptions
			{
				EnableNativeParser = current.Editor.EnableParser,
				PreprocessorDefinitions = current.Compiler.PreprocessorDefine,
				HeadersDirectory = effective.GetIncludeHeadersDirectory(),
				GlobalMacroDirectory = current.Paths.HeadersDirectory,
				BackwardCompatibilityMode = current.Compiler.BackwardCompatibility ? 1 : 0,
				ProgramDirectory = paths.ProgramDirectory,
				ScratchDirectory = paths.ScratchDirectory,
				NativeParserPath = paths.GetResourcePath(NativeParserFileName),
			};
		}
	}
}
