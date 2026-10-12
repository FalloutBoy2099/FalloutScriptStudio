namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.Main;

	// Folders and files to browse for, the message folders and the definitions of the project.
	public sealed partial class ProjectSettingsViewModel
	{
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveMessagePathCommand))]
		[NotifyCanExecuteChangedFor(nameof(MoveMessagePathUpCommand))]
		[NotifyCanExecuteChangedFor(nameof(MoveMessagePathDownCommand))]
		private String? _selectedMessagePath;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AddProjectDefineCommand))]
		private String _newProjectDefine = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveProjectDefineCommand))]
		private String? _selectedProjectDefine;

		[RelayCommand]
		private void BrowseHeadersDirectory()
		{
			String? folder = this._dialogs.PickFolder(Loc.Get("Str.ToolWindows.Settings.SelectHeadersFolder"), this.ExistingDirectory(this.HeadersDirectory.Value));
			if (folder is null)
			{
				return;
			}

			this.HeadersDirectory.Value = folder;
			if (!this.ScriptsHeaderFile.IsOverridden && String.IsNullOrWhiteSpace(this.ScriptsHeaderFile.GlobalValue))
			{
				// As in the global settings: the folder implies the usual scripts.h in it.
				this.ScriptsHeaderFile.IsOverridden = true;
				this.ScriptsHeaderFile.Value = Path.Combine(folder, "SCRIPTS.H");
			}
		}

		[RelayCommand]
		private void BrowseScriptsHeaderFile()
		{
			String? initial = this.ExistingDirectory(Path.GetDirectoryName(this.ScriptsHeaderFile.Value)) ?? this.ExistingDirectory(this.HeadersDirectory.Value);
			String? file = this._dialogs.PickOpenFile(Loc.Get("Str.ToolWindows.Settings.SelectScriptsHeader"), CreateFilter("Str.ToolWindows.Filter.HeaderFiles", "*.h"), initial);
			if (file is not null)
			{
				this.ScriptsHeaderFile.Value = file;
			}
		}

		[RelayCommand]
		private void BrowseOutputDirectory()
		{
			String? folder = this._dialogs.PickFolder(Loc.Get("Str.ToolWindows.Settings.SelectOutputFolder"), this.ExistingDirectory(this.OutputDirectory.Value));
			if (folder is not null)
			{
				this.OutputDirectory.Value = folder;
			}
		}

		[RelayCommand]
		private void BrowseScriptsListFile()
		{
			String? initial = this.ExistingDirectory(Path.GetDirectoryName(this.ScriptsListFile.Value)) ?? this.ExistingDirectory(this.OutputDirectory.EffectiveValue);
			String? file = this._dialogs.PickOpenFile(Loc.Get("Str.ToolWindows.Project.SelectScriptsList"), CreateFilter("Str.ToolWindows.Project.FilterScriptsList", "*.lst"), initial);
			if (file is not null)
			{
				this.ScriptsListFile.Value = file;
			}
		}

		[RelayCommand]
		private void AddMessagePath()
		{
			String? folder = this._dialogs.PickFolder(Loc.Get("Str.ToolWindows.Settings.SelectMessageFolder"), this.ExistingDirectory(null));
			if (String.IsNullOrWhiteSpace(folder))
			{
				return;
			}

			String? existing = this.MessagePaths.FirstOrDefault(path => String.Equals(path, folder, StringComparison.OrdinalIgnoreCase));
			if (existing is null)
			{
				this.MessagePaths.Insert(0, folder);
				existing = folder;
			}

			this.SelectedMessagePath = existing;
		}

		[RelayCommand(CanExecute = nameof(HasSelectedMessagePath))]
		private void RemoveMessagePath()
		{
			if (this.SelectedMessagePath is null)
			{
				return;
			}

			Int32 index = this.MessagePaths.IndexOf(this.SelectedMessagePath);
			if (index < 0)
			{
				return;
			}

			this.MessagePaths.RemoveAt(index);
			this.SelectedMessagePath = this.MessagePaths.Count == 0 ? null : this.MessagePaths[Math.Min(index, this.MessagePaths.Count - 1)];
		}

		[RelayCommand(CanExecute = nameof(CanMoveMessagePathUp))]
		private void MoveMessagePathUp()
		{
			this.MoveSelectedMessagePath(-1);
		}

		[RelayCommand(CanExecute = nameof(CanMoveMessagePathDown))]
		private void MoveMessagePathDown()
		{
			this.MoveSelectedMessagePath(1);
		}

		[RelayCommand(CanExecute = nameof(CanAddProjectDefine))]
		private void AddProjectDefine()
		{
			String define = this.NewProjectDefine.Trim();
			String? existing = this.ProjectDefines.FirstOrDefault(item => String.Equals(item, define, StringComparison.Ordinal));
			if (existing is null)
			{
				this.ProjectDefines.Add(define);
				existing = define;
			}

			this.SelectedProjectDefine = existing;
			this.NewProjectDefine = String.Empty;
		}

		[RelayCommand(CanExecute = nameof(HasSelectedProjectDefine))]
		private void RemoveProjectDefine()
		{
			if (this.SelectedProjectDefine is null)
			{
				return;
			}

			Int32 index = this.ProjectDefines.IndexOf(this.SelectedProjectDefine);
			if (index < 0)
			{
				return;
			}

			if (String.Equals(this.SelectedDefine.Value, this.SelectedProjectDefine, StringComparison.Ordinal))
			{
				// The removed definition can no longer be the selected one.
				this.SelectedDefine.Value = String.Empty;
			}

			this.ProjectDefines.RemoveAt(index);
			this.SelectedProjectDefine = this.ProjectDefines.Count == 0 ? null : this.ProjectDefines[Math.Min(index, this.ProjectDefines.Count - 1)];
		}

		private Boolean HasSelectedMessagePath()
		{
			return this.SelectedMessagePath is not null && this.MessagePathsMode.IsOverridden;
		}

		private Boolean CanMoveMessagePathUp()
		{
			return this.SelectedMessagePath is { } selected && this.MessagePathsMode.IsOverridden && this.MessagePaths.IndexOf(selected) > 0;
		}

		private Boolean CanMoveMessagePathDown()
		{
			return this.SelectedMessagePath is { } selected && this.MessagePathsMode.IsOverridden && this.MessagePaths.IndexOf(selected) is Int32 index && index >= 0 && index < this.MessagePaths.Count - 1;
		}

		private Boolean CanAddProjectDefine()
		{
			String define = this.NewProjectDefine.Trim();
			return PreprocessorDefines.IsValidDefinition(define) && !PreprocessorDefines.BuiltIn.Contains(define, StringComparer.Ordinal);
		}

		private Boolean HasSelectedProjectDefine()
		{
			return this.SelectedProjectDefine is not null;
		}

		private void MoveSelectedMessagePath(Int32 delta)
		{
			if (this.SelectedMessagePath is null)
			{
				return;
			}

			String selected = this.SelectedMessagePath;
			Int32 index = this.MessagePaths.IndexOf(selected);
			Int32 target = index + delta;
			if (index < 0 || target < 0 || target >= this.MessagePaths.Count)
			{
				return;
			}

			this.MessagePaths.Move(index, target);
			this.SelectedMessagePath = selected;
			this.MoveMessagePathUpCommand.NotifyCanExecuteChanged();
			this.MoveMessagePathDownCommand.NotifyCanExecuteChanged();
		}

		/// <summary>Keeps the list the selected definition is chosen from in step with the global definitions and those of the project, without disturbing the selection.</summary>
		private void SynchronizeDefineChoices()
		{
			List<String> wanted = new List<String> { String.Empty };
			wanted.AddRange(this._settings.PreprocessorDefines.GetChoices());
			wanted.AddRange(this.ProjectDefines);
			String selected = this.SelectedDefine.Value;
			if (!String.IsNullOrEmpty(selected) && !wanted.Contains(selected, StringComparer.Ordinal))
			{
				wanted.Add(selected);
			}

			wanted = wanted.Distinct(StringComparer.Ordinal).ToList();
			for (Int32 index = this.DefineChoices.Count - 1; index >= 0; index--)
			{
				if (!wanted.Contains(this.DefineChoices[index].Value, StringComparer.Ordinal))
				{
					this.DefineChoices.RemoveAt(index);
				}
			}

			foreach (String value in wanted)
			{
				if (!this.DefineChoices.Any(option => String.Equals(option.Value, value, StringComparison.Ordinal)))
				{
					this.DefineChoices.Add(new PreprocessorDefineOption(value));
				}
			}
		}
	}
}
