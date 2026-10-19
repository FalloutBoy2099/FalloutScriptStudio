namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.Main;

	// The compile profiles the project adds, and the profile it makes the active one.
	public sealed partial class ProjectSettingsViewModel
	{
		private readonly CompileProfileEditorViewModel _profileEditor;

		/// <summary>Gets the editor of the compile profiles of the project.</summary>
		public CompileProfileEditorViewModel ProfileEditor => this._profileEditor;

		/// <summary>Gets the active compile profile (empty for none).</summary>
		public ProjectOption<String> ActiveProfile { get; private set; } = TextOption(null, null);

		/// <summary>Gets the profiles the active profile can be chosen from (the empty one means none): the global ones and the ones of the project.</summary>
		public ObservableCollection<CompileProfileOption> ActiveProfileChoices { get; } = [];

		private CompileProfileEditorViewModel CreateProfileEditor()
		{
			CompileProfileEditorViewModel editor = new(() => this._settings.PreprocessorDefines.GetChoices().Concat(this.ProjectDefines));
			editor.ProfilesChanged += (_, _) => this.SynchronizeActiveProfileChoices();
			this.ProjectDefines.CollectionChanged += (_, _) => editor.RefreshDefineChoices();
			return editor;
		}

		private void LoadProfiles(AppSettings global, ProjectSettings project)
		{
			this._profileEditor.Load(project.CompileProfiles, global.Compiler);
			this.ActiveProfile = TextOption(global.Compiler.ActiveProfile, project.ActiveCompileProfile);
			this.SynchronizeActiveProfileChoices();
		}

		private void SaveProfiles(ProjectSettings result)
		{
			result.CompileProfiles = this._profileEditor.ToProfiles();
			result.ActiveCompileProfile = TextValue(this.ActiveProfile) is { } name ? this._profileEditor.MapName(name) : null;
		}

		/// <summary>Keeps the list the active profile is chosen from in step with the profiles being edited, without disturbing the selection.</summary>
		private void SynchronizeActiveProfileChoices()
		{
			List<String> wanted =
			[
				String.Empty,
				.. this._profileEditor.Profiles.Select(profile => profile.Name),
				.. this._settings.Settings.Compiler.Profiles.Select(profile => profile.Name),
			];
			wanted = wanted.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

			for (Int32 index = this.ActiveProfileChoices.Count - 1; index >= 0; index--)
			{
				if (!wanted.Contains(this.ActiveProfileChoices[index].Value, StringComparer.OrdinalIgnoreCase))
				{
					this.ActiveProfileChoices.RemoveAt(index);
				}
			}

			foreach (String name in wanted)
			{
				if (!this.ActiveProfileChoices.Any(option => String.Equals(option.Value, name, StringComparison.OrdinalIgnoreCase)))
				{
					this.ActiveProfileChoices.Add(new CompileProfileOption(name));
				}
			}
		}
	}
}