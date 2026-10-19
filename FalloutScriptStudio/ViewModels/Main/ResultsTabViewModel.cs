namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// A closable tab with the results of one search or one "find references" (legacy: every search made its own tab, so the next
	/// refresh, compile or clear of the Errors tab does not overwrite it).
	/// </summary>
	public sealed partial class ResultsTabViewModel : OutputTabViewModel
	{
		private readonly String _titleKey;
		private readonly String? _titleArgument;

		/// <summary>Initializes the tab.</summary>
		/// <param name="output">The output panel.</param>
		/// <param name="titleKey">The localization key of the title.</param>
		/// <param name="titleArgument">The argument of the title, or <see langword="null"/> when the title has none.</param>
		/// <param name="results">The results.</param>
		public ResultsTabViewModel(OutputPanelViewModel output, String titleKey, String? titleArgument, IEnumerable<BuildError> results)
			: base(output)
		{
			ArgumentException.ThrowIfNullOrEmpty(titleKey);
			ArgumentNullException.ThrowIfNull(results);
			this._titleKey = titleKey;
			this._titleArgument = titleArgument;
			foreach (BuildError result in results)
			{
				this.Results.Add(new ErrorItemViewModel(result, String.Empty));
			}

			_ = LanguageChangedSubscription.Subscribe(this, static tab => tab.OnPropertyChanged(nameof(tab.Title)));
		}

		/// <summary>Gets the title, in the current language.</summary>
		public String Title => this._titleArgument is null ? Loc.Get(this._titleKey) : Loc.Format(this._titleKey, this._titleArgument);

		/// <summary>Gets the results.</summary>
		public ObservableCollection<ErrorItemViewModel> Results { get; } = [];

		/// <summary>The selected result.</summary>
		[ObservableProperty]
		public partial ErrorItemViewModel? SelectedResult { get; set; }

		/// <summary>Closes the tab.</summary>
		[RelayCommand]
		public void Close()
		{
			this.Output.CloseResultsTab(this);
		}

		/// <summary>Copies the message of the selected result to the clipboard.</summary>
		[RelayCommand]
		public void CopySelected()
		{
			this.Output.CopyError(this.SelectedResult);
		}
	}
}