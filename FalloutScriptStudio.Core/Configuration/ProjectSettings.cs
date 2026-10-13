namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// The options a mod (a project folder) overrides: the content of <c>fss-project.json</c>. A <see langword="null"/> value means
	/// "inherit the global setting". An instance in memory holds absolute paths; <see cref="ProjectSettingsFile"/> stores them relative
	/// to the project folder so the file can be shared between machines.
	/// </summary>
	/// <remarks>
	/// For the path options and <see cref="PreprocessorDefine"/> an empty text is an override too: "not set" (no headers folder, no define)
	/// whatever the global setting says.
	/// </remarks>
	public sealed class ProjectSettings
	{
		/// <summary>The current version of the file layout.</summary>
		public const Int32 CurrentVersion = 1;

		/// <summary>The name of the project file, found directly in the project folder.</summary>
		public const String FileName = "fss-project.json";

		/// <summary>Gets or sets the version of the file layout this instance was loaded from.</summary>
		public Int32 Version { get; set; } = CurrentVersion;

		/// <summary>Gets or sets the name shown in the status bar and the window title, or <see langword="null"/> for the name of the folder.</summary>
		public String? Name { get; set; }

		/// <summary>Gets or sets the folder with the header files.</summary>
		public String? HeadersDirectory { get; set; }

		/// <summary>Gets or sets the path of <c>scripts.h</c>.</summary>
		public String? ScriptsHeaderFile { get; set; }

		/// <summary>Gets or sets the folder for compiled scripts.</summary>
		public String? OutputDirectory { get; set; }

		/// <summary>Gets or sets a value indicating whether compiled scripts are written next to their sources.</summary>
		public Boolean? CompileToSourceDirectory { get; set; }

		/// <summary>Gets or sets a value indicating whether the headers folder is added to the include search paths.</summary>
		public Boolean? SearchIncludePath { get; set; }

		/// <summary>Gets or sets the preprocessor that runs before the compiler.</summary>
		public PreprocessorKind? Preprocessor { get; set; }

		/// <summary>Gets or sets the selected preprocessor definition (an empty text selects none).</summary>
		public String? PreprocessorDefine { get; set; }

		/// <summary>Gets or sets the definitions of the project; they are offered in the toolbar in addition to the global ones.</summary>
		public List<String> Defines { get; set; } = new List<String>();

		/// <summary>Gets or sets the optimization level.</summary>
		public OptimizationLevel? Optimization { get; set; }

		/// <summary>Gets or sets a value indicating whether short-circuit evaluation is forced for every script.</summary>
		public Boolean? ShortCircuit { get; set; }

		/// <summary>Gets or sets a value indicating whether the backward compatibility mode of the compiler is used.</summary>
		public Boolean? BackwardCompatibility { get; set; }

		/// <summary>Gets or sets a value indicating whether compiler warnings are shown.</summary>
		public Boolean? ShowWarnings { get; set; }

		/// <summary>Gets or sets a value indicating whether additional debug information is produced.</summary>
		public Boolean? DebugInfo { get; set; }

		/// <summary>Gets or sets the folders searched for message files; when set they replace the global list.</summary>
		public List<String>? MessageSearchDirectories { get; set; }

		/// <summary>Gets or sets the path of <c>scripts.lst</c>; when not set it is looked for in the output folder.</summary>
		public String? ScriptsListFile { get; set; }

		/// <summary>Gets or sets the properties of the file this version does not know (kept when the file is written again).</summary>
		[JsonExtensionData]
		public Dictionary<String, JsonElement>? UnknownProperties { get; set; }

		/// <summary>
		/// Creates the settings of a new project: every option holds the value that is in effect now, so the project keeps working the same
		/// way when the global settings change later.
		/// </summary>
		/// <param name="effective">The settings in effect (global settings, and the previous project if any).</param>
		/// <param name="name">The name of the project, or <see langword="null"/>.</param>
		/// <returns>The settings, with absolute paths.</returns>
		public static ProjectSettings CreateFrom(AppSettings effective, String? name)
		{
			ArgumentNullException.ThrowIfNull(effective);
			return new ProjectSettings
			{
				Name = name,
				HeadersDirectory = effective.Paths.HeadersDirectory,
				ScriptsHeaderFile = effective.Paths.ScriptsHeaderFile,
				OutputDirectory = effective.Paths.OutputDirectory,
				CompileToSourceDirectory = effective.Compiler.CompileToSourceDirectory,
				SearchIncludePath = effective.Compiler.SearchIncludePath,
				Preprocessor = effective.Compiler.Preprocessor,
				PreprocessorDefine = effective.Compiler.PreprocessorDefine ?? String.Empty,
				Optimization = effective.Compiler.Optimization,
				ShortCircuit = effective.Compiler.ShortCircuit,
				BackwardCompatibility = effective.Compiler.BackwardCompatibility,
				ShowWarnings = effective.Compiler.ShowWarnings,
				DebugInfo = effective.Compiler.DebugInfo,
				MessageSearchDirectories = effective.Paths.MessageSearchDirectories.ToList(),
			};
		}

		/// <summary>
		/// Lists the options this project overrides: those with a value in the file (an empty text for a path is a value: "not set"), and
		/// <see cref="ProjectOptions.Defines"/> when the project adds definitions.
		/// </summary>
		/// <returns>The identifiers (see <see cref="ProjectOptions"/>), in a stable order.</returns>
		public IReadOnlyList<String> GetOverriddenOptions()
		{
			List<String> options = new List<String>();

			void Add(String id, Boolean overridden)
			{
				if (overridden)
				{
					options.Add(id);
				}
			}

			Add(ProjectOptions.HeadersDirectory, this.HeadersDirectory is not null);
			Add(ProjectOptions.ScriptsHeaderFile, this.ScriptsHeaderFile is not null);
			Add(ProjectOptions.OutputDirectory, this.OutputDirectory is not null);
			Add(ProjectOptions.CompileToSourceDirectory, this.CompileToSourceDirectory.HasValue);
			Add(ProjectOptions.SearchIncludePath, this.SearchIncludePath.HasValue);
			Add(ProjectOptions.Preprocessor, this.Preprocessor.HasValue);
			Add(ProjectOptions.PreprocessorDefine, this.PreprocessorDefine is not null);
			Add(ProjectOptions.Defines, this.Defines is { Count: > 0 });
			Add(ProjectOptions.Optimization, this.Optimization.HasValue);
			Add(ProjectOptions.ShortCircuit, this.ShortCircuit.HasValue);
			Add(ProjectOptions.BackwardCompatibility, this.BackwardCompatibility.HasValue);
			Add(ProjectOptions.ShowWarnings, this.ShowWarnings.HasValue);
			Add(ProjectOptions.DebugInfo, this.DebugInfo.HasValue);
			Add(ProjectOptions.MessageSearchDirectories, this.MessageSearchDirectories is not null);
			Add(ProjectOptions.ScriptsListFile, this.ScriptsListFile is not null);
			return options;
		}

		/// <summary>
		/// Creates a deep copy.
		/// </summary>
		/// <returns>The copy.</returns>
		public ProjectSettings Clone()
		{
			ProjectSettings copy = (ProjectSettings)this.MemberwiseClone();
			copy.Defines = (this.Defines ?? new List<String>()).ToList();
			copy.MessageSearchDirectories = this.MessageSearchDirectories?.ToList();
			copy.UnknownProperties = this.UnknownProperties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
			return copy;
		}

		/// <summary>
		/// Repairs values that are missing or unusable (the file may have been edited by hand).
		/// </summary>
		public void Normalize()
		{
			this.Version = Math.Max(this.Version, CurrentVersion);
			this.Name = String.IsNullOrWhiteSpace(this.Name) ? null : this.Name.Trim();
			this.Defines = (this.Defines ?? new List<String>())
				.Where(PreprocessorDefines.IsValidDefinition)
				.Select(item => item.Trim())
				.Distinct(StringComparer.Ordinal)
				.ToList();

			if (this.PreprocessorDefine is not null)
			{
				this.PreprocessorDefine = PreprocessorDefines.IsValidDefinition(this.PreprocessorDefine) ? this.PreprocessorDefine.Trim() : String.Empty;
			}

			if (this.MessageSearchDirectories is not null)
			{
				this.MessageSearchDirectories = this.MessageSearchDirectories
					.Where(path => !String.IsNullOrWhiteSpace(path))
					.Select(path => path.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
		}

		/// <summary>
		/// Applies a conversion to every path of the settings (the paths are made relative or absolute with it).
		/// </summary>
		/// <param name="convert">The conversion; it receives a path that is not blank.</param>
		/// <returns>The converted copy.</returns>
		internal ProjectSettings ConvertPaths(Func<String, String> convert)
		{
			String? Convert(String? path)
			{
				return String.IsNullOrWhiteSpace(path) ? path : convert(path);
			}

			ProjectSettings copy = this.Clone();
			copy.HeadersDirectory = Convert(copy.HeadersDirectory);
			copy.ScriptsHeaderFile = Convert(copy.ScriptsHeaderFile);
			copy.OutputDirectory = Convert(copy.OutputDirectory);
			copy.ScriptsListFile = Convert(copy.ScriptsListFile);
			copy.MessageSearchDirectories = copy.MessageSearchDirectories?.Select(path => Convert(path) ?? path).ToList();
			return copy;
		}
	}
}
