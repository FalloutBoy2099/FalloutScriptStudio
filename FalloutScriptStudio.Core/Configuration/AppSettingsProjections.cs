namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Builds the immutable options of the other Core services from the user settings.
	/// </summary>
	public static class AppSettingsProjections
	{
		/// <summary>
		/// Creates the compiler options.
		/// </summary>
		/// <param name="settings">The user settings.</param>
		/// <param name="paths">The paths of the installation.</param>
		/// <returns>The options.</returns>
		public static CompilerOptions ToCompilerOptions(this AppSettings settings, AppPaths paths)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(paths);

			return new CompilerOptions
			{
				ProgramDirectory = paths.ProgramDirectory,
				ResourcesDirectory = paths.ResourcesDirectory,
				ScratchDirectory = paths.ScratchDirectory,
				Optimization = settings.Compiler.Optimization,
				ShowWarnings = settings.Compiler.ShowWarnings,
				DebugInfo = settings.Compiler.DebugInfo,
				ShortCircuit = settings.Compiler.ShortCircuit,
				BackwardCompatibility = settings.Compiler.BackwardCompatibility,
				Preprocessor = settings.Compiler.Preprocessor,
				PreprocessorDefine = settings.Compiler.PreprocessorDefine,
				SearchIncludePath = settings.Compiler.SearchIncludePath,
				ScriptsHeaderFile = settings.Paths.ScriptsHeaderFile,
				HeadersDirectory = settings.Paths.HeadersDirectory,
				CompileToSourceDirectory = settings.Compiler.CompileToSourceDirectory,
				OutputDirectory = settings.Paths.OutputDirectory,
				UseUserCommand = settings.Compiler.UseUserCommand,
				Decompiler = settings.Compiler.Decompiler,
				ExternalDecompilerPath = settings.Compiler.ExternalDecompilerPath,
				DecompileForFallout1 = settings.Compiler.DecompileForFallout1,
				UseLegacyDecompiler = settings.Compiler.UseLegacyDecompiler,
				TabsToSpaces = settings.Editor.TabsToSpaces,
				TabSize = settings.Editor.TabSize,
			};
		}

		/// <summary>
		/// Gets the headers folder that takes part in <c>#include</c> resolution (legacy <c>Settings.IsSearchIncludes ? pathHeadersFiles : null</c>).
		/// </summary>
		/// <param name="settings">The user settings.</param>
		/// <returns>The folder, or <see langword="null"/> when "search includes" is off or no <c>scripts.h</c> is configured.</returns>
		public static String? GetIncludeHeadersDirectory(this AppSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);
			return IncludeSearch.GetHeadersDirectory(settings.Compiler.SearchIncludePath, settings.Paths.ScriptsHeaderFile, settings.Paths.HeadersDirectory);
		}

		/// <summary>
		/// Creates the resolver that finds the files of <c>#include</c> directives; the headers folder takes part only when the include search is enabled.
		/// </summary>
		/// <param name="settings">The user settings.</param>
		/// <param name="paths">The paths of the installation.</param>
		/// <returns>The resolver.</returns>
		public static IncludeResolver ToIncludeResolver(this AppSettings settings, AppPaths paths)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(paths);
			return new IncludeResolver(settings.GetIncludeHeadersDirectory(), paths.ProgramDirectory);
		}

		/// <summary>
		/// Creates the options of a mass compilation.
		/// </summary>
		/// <param name="settings">The user settings.</param>
		/// <returns>The options.</returns>
		public static BatchCompileOptions ToBatchCompileOptions(this AppSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);
			return new BatchCompileOptions { MultiThreaded = settings.Compiler.MultiThreadedBatch };
		}

		/// <summary>
		/// Creates the options for locating and reading message files.
		/// </summary>
		/// <param name="settings">The user settings.</param>
		/// <returns>The options.</returns>
		public static MessageFileOptions ToMessageFileOptions(this AppSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			return new MessageFileOptions
			{
				OutputDirectory = settings.Paths.OutputDirectory,
				Language = settings.Messages.Language,
				AssociateByScriptId = settings.Messages.AssociateByScriptId,
				SearchDirectories = settings.Paths.MessageSearchDirectories.ToArray(),
				Encoding = settings.Editor.FileEncoding.ToEncoding(),
			};
		}
	}
}
