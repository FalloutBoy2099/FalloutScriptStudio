namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;

	// Compiler page: the compile profiles (named sets of compiler options).
	public sealed partial class SettingsViewModel
	{
		private CompileProfileEditorViewModel? _profileEditor;

		/// <summary>Gets the editor of the global compile profiles.</summary>
		public CompileProfileEditorViewModel ProfileEditor => this._profileEditor ??= this.CreateProfileEditor();

		private CompileProfileEditorViewModel CreateProfileEditor()
		{
			CompileProfileEditorViewModel editor = new(() => PreprocessorDefines.BuiltIn.Concat(this.PreprocessorDefineItems));
			editor.Load(this.Draft.Compiler.Profiles, this.Draft.Compiler);
			this.PreprocessorDefineItems.CollectionChanged += (_, _) => editor.RefreshDefineChoices();
			return editor;
		}

		/// <summary>Hands the edited profiles to the draft; the profile that is chosen follows when it was renamed.</summary>
		private void CommitProfiles()
		{
			if (this._profileEditor is not { } editor)
			{
				return;
			}

			this.Draft.Compiler.Profiles = editor.ToProfiles();
			String? active = this._settingsService.Settings.Compiler.ActiveProfile;
			String? mapped = editor.MapName(active);
			if (!String.Equals(active, mapped, StringComparison.Ordinal))
			{
				// The chosen profile is not an option of this window (the toolbar writes it): it is updated on its own.
				this._settingsService.Update(live => live.Compiler.ActiveProfile = mapped);
			}
		}
	}
}