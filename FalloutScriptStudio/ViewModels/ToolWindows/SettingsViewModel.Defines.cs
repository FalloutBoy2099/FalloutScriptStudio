namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.ObjectModel;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;

	// Compiler page: the preprocessor definitions offered by the build toolbar.
	public sealed partial class SettingsViewModel
	{
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AddPreprocessorDefineCommand))]
		private String _newPreprocessorDefine = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemovePreprocessorDefineCommand))]
		private String? _selectedPreprocessorDefine;

		/// <summary>Gets the definitions added by the user (<c>DEBUG</c> and <c>RELEASE</c> are always offered and not listed).</summary>
		public ObservableCollection<String> PreprocessorDefineItems { get; }

		[RelayCommand(CanExecute = nameof(CanAddPreprocessorDefine))]
		private void AddPreprocessorDefine()
		{
			String define = this.NewPreprocessorDefine.Trim();
			String? existing = this.PreprocessorDefineItems.FirstOrDefault(item => String.Equals(item, define, StringComparison.Ordinal));
			if (existing is null)
			{
				this.PreprocessorDefineItems.Add(define);
				existing = define;
			}

			this.SelectedPreprocessorDefine = existing;
			this.NewPreprocessorDefine = String.Empty;
		}

		[RelayCommand(CanExecute = nameof(HasSelectedPreprocessorDefine))]
		private void RemovePreprocessorDefine()
		{
			if (this.SelectedPreprocessorDefine is null)
			{
				return;
			}

			Int32 index = this.PreprocessorDefineItems.IndexOf(this.SelectedPreprocessorDefine);
			if (index < 0)
			{
				return;
			}

			this.PreprocessorDefineItems.RemoveAt(index);
			this.SelectedPreprocessorDefine = this.PreprocessorDefineItems.Count == 0 ? null : this.PreprocessorDefineItems[Math.Min(index, this.PreprocessorDefineItems.Count - 1)];
		}

		private Boolean CanAddPreprocessorDefine()
		{
			String define = this.NewPreprocessorDefine.Trim();
			return PreprocessorDefines.IsValidDefinition(define) && !PreprocessorDefines.BuiltIn.Contains(define, StringComparer.Ordinal);
		}

		private Boolean HasSelectedPreprocessorDefine()
		{
			return this.SelectedPreprocessorDefine is not null;
		}

		private void CommitPreprocessorDefines()
		{
			PreprocessorDefines target = this._settingsService.PreprocessorDefines;
			target.Items = this.PreprocessorDefineItems.ToList();
			target.Sanitize();
		}
	}
}
