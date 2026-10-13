namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// The settings that are really in effect: the global settings with the overrides of the active project on top. This is the one place
	/// where the two are combined; every consumer of the compiler, header, include, define and message folder options reads them from here
	/// (<see cref="ISettingsService.Effective"/>) and not from <see cref="AppSettings"/> directly.
	/// </summary>
	public sealed class EffectiveSettings
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="EffectiveSettings"/> class.
		/// </summary>
		/// <param name="global">The global settings.</param>
		/// <param name="globalDefines">The global list of preprocessor definitions.</param>
		/// <param name="projectFolder">The folder of the active project, or <see langword="null"/> when no project is open.</param>
		/// <param name="project">The settings of the project (absolute paths), or <see langword="null"/> when there is no project or it has no settings file.</param>
		public EffectiveSettings(AppSettings global, PreprocessorDefines globalDefines, String? projectFolder = null, ProjectSettings? project = null)
		{
			ArgumentNullException.ThrowIfNull(global);
			ArgumentNullException.ThrowIfNull(globalDefines);

			this.ProjectFolder = projectFolder;
			this.Project = project;
			this.Settings = project is null ? global : Combine(global, project);
			this.ScriptsListFile = project?.ScriptsListFile is { } list && !String.IsNullOrWhiteSpace(list) ? list : null;
			this.DefineChoices = globalDefines.GetChoices()
				.Concat(project?.Defines ?? Enumerable.Empty<String>())
				.Distinct(StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>Gets the settings in effect. It is the global instance itself when the project overrides nothing; do not modify it.</summary>
		public AppSettings Settings { get; }

		/// <summary>Gets the folder of the active project, or <see langword="null"/>.</summary>
		public String? ProjectFolder { get; }

		/// <summary>Gets the overrides of the active project, or <see langword="null"/> when there is no project settings file.</summary>
		public ProjectSettings? Project { get; }

		/// <summary>Gets the path of <c>scripts.lst</c> the project names, or <see langword="null"/> to look for it in the output folder.</summary>
		public String? ScriptsListFile { get; }

		/// <summary>Gets the definitions the toolbar offers: the built-in ones, the global ones and the ones of the project.</summary>
		public IReadOnlyList<String> DefineChoices { get; }

		/// <summary>Gets the name of the active project (the name in its settings, otherwise the name of its folder), or an empty text when no project is open.</summary>
		public String ProjectName
		{
			get
			{
				if (this.ProjectFolder is not { Length: > 0 } folder)
				{
					return String.Empty;
				}

				if (this.Project?.Name is { Length: > 0 } name)
				{
					return name;
				}

				String leaf = System.IO.Path.GetFileName(folder);
				return leaf.Length > 0 ? leaf : folder;
			}
		}

		/// <summary>
		/// Lists the options the active project overrides, so that the Settings window can tell that the global value of an option is not in
		/// effect while the project is open.
		/// </summary>
		/// <returns>The identifiers (see <see cref="ProjectOptions"/>); empty when no project is open or it overrides nothing.</returns>
		public IReadOnlyList<String> GetOverriddenOptions()
		{
			return this.ProjectFolder is null || this.Project is null ? Array.Empty<String>() : this.Project.GetOverriddenOptions();
		}

		/// <summary>Creates the compiler options.</summary>
		/// <param name="paths">The paths of the installation.</param>
		/// <returns>The options.</returns>
		public CompilerOptions ToCompilerOptions(AppPaths paths)
		{
			return this.Settings.ToCompilerOptions(paths);
		}

		/// <summary>Gets the headers folder that takes part in <c>#include</c> resolution.</summary>
		/// <returns>The folder, or <see langword="null"/>.</returns>
		public String? GetIncludeHeadersDirectory()
		{
			return this.Settings.GetIncludeHeadersDirectory();
		}

		/// <summary>Creates the resolver of <c>#include</c> files.</summary>
		/// <param name="paths">The paths of the installation.</param>
		/// <returns>The resolver.</returns>
		public IncludeResolver ToIncludeResolver(AppPaths paths)
		{
			return this.Settings.ToIncludeResolver(paths);
		}

		/// <summary>Creates the options for locating and reading message files.</summary>
		/// <returns>The options.</returns>
		public MessageFileOptions ToMessageFileOptions()
		{
			return this.Settings.ToMessageFileOptions();
		}

		private static AppSettings Combine(AppSettings global, ProjectSettings project)
		{
			PathSettings paths = global.Paths.Copy();
			CompilerSettings compiler = global.Compiler.Copy();

			paths.HeadersDirectory = Pick(project.HeadersDirectory, paths.HeadersDirectory);
			paths.ScriptsHeaderFile = Pick(project.ScriptsHeaderFile, paths.ScriptsHeaderFile);
			paths.OutputDirectory = Pick(project.OutputDirectory, paths.OutputDirectory);
			if (project.MessageSearchDirectories is not null)
			{
				paths.MessageSearchDirectories = project.MessageSearchDirectories.ToList();
			}

			compiler.CompileToSourceDirectory = project.CompileToSourceDirectory ?? compiler.CompileToSourceDirectory;
			compiler.SearchIncludePath = project.SearchIncludePath ?? compiler.SearchIncludePath;
			compiler.Preprocessor = project.Preprocessor ?? compiler.Preprocessor;
			compiler.PreprocessorDefine = Pick(project.PreprocessorDefine, compiler.PreprocessorDefine);
			compiler.Optimization = project.Optimization ?? compiler.Optimization;
			compiler.ShortCircuit = project.ShortCircuit ?? compiler.ShortCircuit;
			compiler.BackwardCompatibility = project.BackwardCompatibility ?? compiler.BackwardCompatibility;
			compiler.ShowWarnings = project.ShowWarnings ?? compiler.ShowWarnings;
			compiler.DebugInfo = project.DebugInfo ?? compiler.DebugInfo;

			return new AppSettings
			{
				Version = global.Version,
				IsFirstRunCompleted = global.IsFirstRunCompleted,
				Paths = paths,
				Compiler = compiler,
				Editor = global.Editor,
				Messages = global.Messages,
				Flowchart = global.Flowchart,
				Window = global.Window,
			};
		}

		/// <summary>The override wins; a blank override means "not set"; no override inherits.</summary>
		private static String? Pick(String? overrideValue, String? globalValue)
		{
			if (overrideValue is null)
			{
				return globalValue;
			}

			return String.IsNullOrWhiteSpace(overrideValue) ? null : overrideValue;
		}
	}
}
