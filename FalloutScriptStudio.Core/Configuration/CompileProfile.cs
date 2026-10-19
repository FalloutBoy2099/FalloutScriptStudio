namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;
	using Compilation;

	/// <summary>
	/// A named set of compiler options (for example "Debug" or "Release"). A profile defines only some of the options: the ones it leaves
	/// <see langword="null"/> keep the value of the global compiler options (and of the project). The active profile is applied on top of them.
	/// </summary>
	public sealed class CompileProfile
	{
		/// <summary>The longest name a profile can have.</summary>
		public const Int32 MaxNameLength = 40;

		/// <summary>Gets or sets the name of the profile, unique (ignoring case) among the profiles in effect.</summary>
		public String Name { get; set; } = String.Empty;

		/// <summary>Gets or sets the optimization level, or <see langword="null"/> to keep the one of the settings.</summary>
		public OptimizationLevel? Optimization { get; set; }

		/// <summary>Gets or sets whether short-circuit evaluation is forced, or <see langword="null"/> to keep the setting.</summary>
		public Boolean? ShortCircuit { get; set; }

		/// <summary>Gets or sets whether the backward compatibility mode is used, or <see langword="null"/> to keep the setting.</summary>
		public Boolean? BackwardCompatibility { get; set; }

		/// <summary>Gets or sets whether debug information is produced, or <see langword="null"/> to keep the setting.</summary>
		public Boolean? DebugInfo { get; set; }

		/// <summary>Gets or sets the preprocessor definition (an empty text means "no definition"), or <see langword="null"/> to keep the definition chosen in the toolbar.</summary>
		public String? PreprocessorDefine { get; set; }

		/// <summary>Gets or sets the properties of the file this version does not know (kept when the file is written again).</summary>
		[JsonExtensionData]
		public Dictionary<String, JsonElement>? UnknownProperties { get; set; }

		/// <summary>Tells whether a text can be the name of a profile: not blank, not longer than <see cref="MaxNameLength"/>, without control characters.</summary>
		/// <param name="name">The text.</param>
		/// <returns><see langword="true"/> when the name is usable.</returns>
		public static Boolean IsValidName(String? name)
		{
			return !String.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaxNameLength && !name.Any(Char.IsControl);
		}

		/// <summary>Creates the profiles a new installation starts with: "Debug" and "Release".</summary>
		/// <returns>The profiles.</returns>
		public static List<CompileProfile> CreateDefaults()
		{
			return
			[
				new CompileProfile { Name = "Debug", Optimization = OptimizationLevel.None, DebugInfo = true, PreprocessorDefine = "DEBUG" },
				new CompileProfile { Name = "Release", Optimization = OptimizationLevel.Full, DebugInfo = false, PreprocessorDefine = "RELEASE" },
			];
		}

		/// <summary>Finds a profile by name, ignoring case.</summary>
		/// <param name="profiles">The profiles.</param>
		/// <param name="name">The name, or <see langword="null"/>.</param>
		/// <returns>The profile, or <see langword="null"/> when the name is empty or unknown.</returns>
		public static CompileProfile? Find(IEnumerable<CompileProfile> profiles, String? name)
		{
			ArgumentNullException.ThrowIfNull(profiles);
			return String.IsNullOrWhiteSpace(name) ? null : profiles.FirstOrDefault(profile => String.Equals(profile.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Cleans a list of profiles: unusable names are dropped, names are trimmed, a name that appears twice keeps its first profile.</summary>
		/// <param name="profiles">The profiles read from a file; may be <see langword="null"/>.</param>
		/// <returns>The repaired list.</returns>
		public static List<CompileProfile> Normalize(IEnumerable<CompileProfile?>? profiles)
		{
			List<CompileProfile> result = [];
			foreach (CompileProfile? profile in profiles ?? Enumerable.Empty<CompileProfile?>())
			{
				if (profile is null || !IsValidName(profile.Name) || Find(result, profile.Name) is not null)
				{
					continue;
				}

				CompileProfile copy = profile.Clone();
				copy.Name = profile.Name.Trim();
				if (copy.PreprocessorDefine is not null)
				{
					copy.PreprocessorDefine = PreprocessorDefines.IsValidDefinition(copy.PreprocessorDefine) ? copy.PreprocessorDefine.Trim() : String.Empty;
				}

				result.Add(copy);
			}

			return result;
		}

		/// <summary>Creates a copy that can be changed without touching this instance.</summary>
		/// <returns>The copy.</returns>
		public CompileProfile Clone()
		{
			CompileProfile copy = (CompileProfile)this.MemberwiseClone();
			copy.UnknownProperties = this.UnknownProperties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
			return copy;
		}

		/// <summary>Applies the options the profile defines to a set of compiler settings.</summary>
		/// <param name="compiler">The settings, changed in place (a copy owned by the caller).</param>
		public void ApplyTo(CompilerSettings compiler)
		{
			ArgumentNullException.ThrowIfNull(compiler);
			compiler.Optimization = this.Optimization ?? compiler.Optimization;
			compiler.ShortCircuit = this.ShortCircuit ?? compiler.ShortCircuit;
			compiler.BackwardCompatibility = this.BackwardCompatibility ?? compiler.BackwardCompatibility;
			compiler.DebugInfo = this.DebugInfo ?? compiler.DebugInfo;
			if (this.PreprocessorDefine is not null)
			{
				compiler.PreprocessorDefine = String.IsNullOrWhiteSpace(this.PreprocessorDefine) ? null : this.PreprocessorDefine;
			}
		}
	}
}