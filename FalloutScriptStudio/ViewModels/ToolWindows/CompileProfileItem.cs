namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using Core.Compilation;
	using Core.Configuration;

	/// <summary>
	/// One compile profile being edited: its name, and for each option the choice "not set (keep the setting) / set" and the value. While an
	/// option is not set the control shows the value the profile would not change.
	/// </summary>
	public sealed class CompileProfileItem : ObservableObject
	{
		private readonly CompileProfileEditorViewModel _owner;
		private readonly CompileProfile _source;
		private String _name;

		/// <summary>Initializes a new instance of the <see cref="CompileProfileItem"/> class.</summary>
		/// <param name="owner">The editor the profile belongs to (it decides whether a name is free).</param>
		/// <param name="profile">The profile to edit; it is copied.</param>
		/// <param name="baseline">The options the profile is applied on top of; they are shown for the options the profile does not set.</param>
		public CompileProfileItem(CompileProfileEditorViewModel owner, CompileProfile profile, CompilerSettings baseline)
		{
			ArgumentNullException.ThrowIfNull(owner);
			ArgumentNullException.ThrowIfNull(profile);
			ArgumentNullException.ThrowIfNull(baseline);

			this._owner = owner;
			this._source = profile.Clone();
			this._name = profile.Name;
			this.OriginalName = profile.Name;
			this.Optimization = new ProjectOption<OptimizationLevel>(baseline.Optimization, profile.Optimization.HasValue, profile.Optimization ?? baseline.Optimization);
			this.ShortCircuit = new ProjectOption<Boolean>(baseline.ShortCircuit, profile.ShortCircuit.HasValue, profile.ShortCircuit ?? baseline.ShortCircuit);
			this.BackwardCompatibility = new ProjectOption<Boolean>(baseline.BackwardCompatibility, profile.BackwardCompatibility.HasValue, profile.BackwardCompatibility ?? baseline.BackwardCompatibility);
			this.DebugInfo = new ProjectOption<Boolean>(baseline.DebugInfo, profile.DebugInfo.HasValue, profile.DebugInfo ?? baseline.DebugInfo);
			this.PreprocessorDefine = new ProjectOption<String>(baseline.PreprocessorDefine ?? String.Empty, profile.PreprocessorDefine is not null, profile.PreprocessorDefine ?? baseline.PreprocessorDefine ?? String.Empty);
		}

		/// <summary>Gets the name the profile had when the editor was opened (the settings may refer to it).</summary>
		public String OriginalName { get; }

		/// <summary>Gets the text a screen reader announces for the profile in the list (the automation name of a list item is the text of its object).</summary>
		/// <returns>The name.</returns>
		public override String ToString()
		{
			return this._name;
		}

		/// <summary>
		/// Gets or sets the name. A name that is blank, too long or used by another profile is refused and the previous name stays (the
		/// control shows it again).
		/// </summary>
		public String Name
		{
			get => this._name;
			set
			{
				String trimmed = (value ?? String.Empty).Trim();
				if (!String.Equals(trimmed, this._name, StringComparison.Ordinal) && CompileProfile.IsValidName(trimmed) && !this._owner.IsNameTaken(trimmed, this))
				{
					this._name = trimmed;
					this.OnPropertyChanged();
					this._owner.NotifyProfilesChanged();
					return;
				}

				// Raised even when the name is refused: the text box goes back to the name in effect.
				this.OnPropertyChanged();
			}
		}

		/// <summary>Gets the optimization level.</summary>
		public ProjectOption<OptimizationLevel> Optimization { get; }

		/// <summary>Gets the option "short-circuit evaluation".</summary>
		public ProjectOption<Boolean> ShortCircuit { get; }

		/// <summary>Gets the option "backward compatibility".</summary>
		public ProjectOption<Boolean> BackwardCompatibility { get; }

		/// <summary>Gets the option "debug information".</summary>
		public ProjectOption<Boolean> DebugInfo { get; }

		/// <summary>Gets the preprocessor definition (empty for none).</summary>
		public ProjectOption<String> PreprocessorDefine { get; }

		/// <summary>Builds the profile the item describes; the properties of the file this version does not know are kept.</summary>
		/// <returns>The profile.</returns>
		public CompileProfile ToProfile()
		{
			CompileProfile profile = this._source.Clone();
			profile.Name = this._name;
			profile.Optimization = this.Optimization.IsOverridden ? this.Optimization.Value : null;
			profile.ShortCircuit = this.ShortCircuit.IsOverridden ? this.ShortCircuit.Value : null;
			profile.BackwardCompatibility = this.BackwardCompatibility.IsOverridden ? this.BackwardCompatibility.Value : null;
			profile.DebugInfo = this.DebugInfo.IsOverridden ? this.DebugInfo.Value : null;
			profile.PreprocessorDefine = this.PreprocessorDefine.IsOverridden ? (this.PreprocessorDefine.Value ?? String.Empty).Trim() : null;
			return profile;
		}
	}
}