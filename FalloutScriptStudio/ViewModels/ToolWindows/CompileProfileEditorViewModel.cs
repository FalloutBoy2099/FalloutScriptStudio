namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using Core.Compilation;
	using Core.Configuration;
	using Localization;

	/// <summary>
	/// The editor of a list of compile profiles (named sets of compiler options): add, rename, delete, and set the options of each. It is
	/// shared by the Compiler page of the Settings window (the global profiles) and the project settings window (the profiles of the project).
	/// </summary>
	public sealed partial class CompileProfileEditorViewModel : ObservableObject
	{
		private readonly Func<IEnumerable<String>> _defineChoices;
		private CompilerSettings _baseline = new();

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasSelection))]
		[NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
		private CompileProfileItem? _selectedProfile;

		/// <summary>Initializes a new instance of the <see cref="CompileProfileEditorViewModel"/> class.</summary>
		/// <param name="defineChoices">Gives the preprocessor definitions a profile can choose from (the toolbar list).</param>
		public CompileProfileEditorViewModel(Func<IEnumerable<String>> defineChoices)
		{
			ArgumentNullException.ThrowIfNull(defineChoices);
			this._defineChoices = defineChoices;
		}

		/// <summary>Raised when a profile was added, deleted or renamed (the lists of profile names that depend on it are refreshed).</summary>
		public event EventHandler? ProfilesChanged;

		/// <summary>Gets the profiles being edited.</summary>
		public ObservableCollection<CompileProfileItem> Profiles { get; } = [];

		/// <summary>Gets the preprocessor definitions a profile can choose from.</summary>
		public ObservableCollection<String> DefineChoices { get; } = [];

		/// <summary>Gets the optimization levels to choose from.</summary>
		public IReadOnlyList<Choice<OptimizationLevel>> OptimizationChoices { get; } = new List<Choice<OptimizationLevel>>
		{
			new(OptimizationLevel.None, Loc.Get("Str.ToolWindows.Choice.Optimization.None")),
			new(OptimizationLevel.Basic, Loc.Get("Str.ToolWindows.Choice.Optimization.Basic")),
			new(OptimizationLevel.Full, Loc.Get("Str.ToolWindows.Choice.Optimization.Full")),
			new(OptimizationLevel.Experimental, Loc.Get("Str.ToolWindows.Choice.Optimization.Experimental")),
		};

		/// <summary>Gets a value indicating whether a profile is selected.</summary>
		public Boolean HasSelection => this.SelectedProfile is not null;

		/// <summary>Replaces the profiles being edited.</summary>
		/// <param name="profiles">The profiles.</param>
		/// <param name="baseline">The options the profiles are applied on top of (shown for the options a profile does not set).</param>
		public void Load(IEnumerable<CompileProfile> profiles, CompilerSettings baseline)
		{
			ArgumentNullException.ThrowIfNull(profiles);
			ArgumentNullException.ThrowIfNull(baseline);

			this._baseline = baseline;
			this.Profiles.Clear();
			foreach (CompileProfile profile in CompileProfile.Normalize(profiles))
			{
				this.Profiles.Add(new CompileProfileItem(this, profile, baseline));
			}

			this.SelectedProfile = this.Profiles.FirstOrDefault();
			this.RefreshDefineChoices();
			this.NotifyProfilesChanged();
		}

		/// <summary>Reads the list of definitions again (the host added or removed one).</summary>
		public void RefreshDefineChoices()
		{
			List<String> wanted = this._defineChoices().Where(PreprocessorDefines.IsValidDefinition).Select(item => item.Trim()).Distinct(StringComparer.Ordinal).ToList();
			foreach (CompileProfileItem item in this.Profiles)
			{
				String define = item.PreprocessorDefine.Value;
				if (!String.IsNullOrEmpty(define) && !wanted.Contains(define, StringComparer.Ordinal))
				{
					wanted.Add(define);
				}
			}

			if (!wanted.SequenceEqual(this.DefineChoices, StringComparer.Ordinal))
			{
				this.DefineChoices.Clear();
				foreach (String define in wanted)
				{
					this.DefineChoices.Add(define);
				}
			}
		}

		/// <summary>Builds the profiles the editor describes.</summary>
		/// <returns>The profiles, with unique usable names.</returns>
		public List<CompileProfile> ToProfiles()
		{
			return CompileProfile.Normalize(this.Profiles.Select(item => item.ToProfile()));
		}

		/// <summary>Maps the name of a profile as it was when the editor was opened to its name now, so that a profile that was renamed keeps being the chosen one.</summary>
		/// <param name="name">The name stored in the settings, or <see langword="null"/>.</param>
		/// <returns>The new name when the profile is in this list; otherwise the name unchanged (it may belong to another list, or the profile was deleted).</returns>
		public String? MapName(String? name)
		{
			if (String.IsNullOrWhiteSpace(name))
			{
				return name;
			}

			CompileProfileItem? item = this.Profiles.FirstOrDefault(profile => String.Equals(profile.OriginalName, name.Trim(), StringComparison.OrdinalIgnoreCase));
			return item?.Name ?? name;
		}

		/// <summary>Tells whether another profile already has a name (ignoring case).</summary>
		/// <param name="name">The name.</param>
		/// <param name="except">The profile that wants the name.</param>
		/// <returns><see langword="true"/> when the name is taken.</returns>
		public Boolean IsNameTaken(String name, CompileProfileItem except)
		{
			return this.Profiles.Any(profile => !ReferenceEquals(profile, except) && String.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Tells the host that the names of the profiles changed.</summary>
		internal void NotifyProfilesChanged()
		{
			this.ProfilesChanged?.Invoke(this, EventArgs.Empty);
		}

		[RelayCommand]
		private void AddProfile()
		{
			String baseName = Loc.Get("Str.ToolWindows.Profiles.NewName");
			String name = baseName;
			for (Int32 number = 2; this.Profiles.Any(profile => String.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)); number++)
			{
				name = baseName + " " + number.ToString(CultureInfo.InvariantCulture);
			}

			CompileProfileItem item = new(this, new CompileProfile { Name = name }, this._baseline);
			this.Profiles.Add(item);
			this.SelectedProfile = item;
			this.RefreshDefineChoices();
			this.NotifyProfilesChanged();
		}

		[RelayCommand(CanExecute = nameof(HasSelection))]
		private void DeleteProfile()
		{
			if (this.SelectedProfile is not { } selected)
			{
				return;
			}

			Int32 index = this.Profiles.IndexOf(selected);
			this.Profiles.Remove(selected);
			this.SelectedProfile = this.Profiles.Count == 0 ? null : this.Profiles[Math.Min(index, this.Profiles.Count - 1)];
			this.NotifyProfilesChanged();
		}
	}
}