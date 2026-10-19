namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace;

	/// <summary>The tabs of the panel: search and reference results, build errors, texts and tab selection.</summary>
	public sealed partial class OutputPanelViewModel
	{
		public void ShowSearchResults(IReadOnlyList<BuildError> results, String titleKey, String? titleArgument = null)
		{
			ArgumentNullException.ThrowIfNull(results);
			ArgumentException.ThrowIfNullOrEmpty(titleKey);

			ResultsTabViewModel tab = new(this, titleKey, titleArgument, results);
			this.Tabs.Add(tab);
			this.SelectedTab = this.Tabs.Count - 1;
			this.StatusMessage = Loc.Format("Str.Shell.Output.SearchStatus", tab.Title, results.Count);
			this.ShowRequested?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Closes a search or references tab and selects the tab before it.</summary>
		/// <param name="tab">The tab.</param>
		public void CloseResultsTab(ResultsTabViewModel tab)
		{
			ArgumentNullException.ThrowIfNull(tab);

			Int32 index = this.Tabs.IndexOf(tab);
			if (index <= (Int32)OutputTab.Errors)
			{
				return;
			}

			Boolean wasSelected = this.SelectedTab == index;
			this.Tabs.RemoveAt(index);
			if (wasSelected)
			{
				this.SelectedTab = index - 1;
			}
			else if (this.SelectedTab > index)
			{
				this.SelectedTab--;
			}
		}

		public void ShowBuildErrors(IReadOnlyList<BuildError> errors)
		{
			ArgumentNullException.ThrowIfNull(errors);
			this._aggregatedErrors = errors;
			this._hideDocumentRows = false;
			this.RebuildErrors();
		}

		public void ReportStatus(String message)
		{
			this.StatusMessage = message;
		}

		public void SetBuildText(String text)
		{
			this.BuildText = text;
		}

		public void AppendBuildLine(String line)
		{
			this.BuildText = this.BuildText.Length == 0 ? line : this.BuildText + "\r\n" + line;
		}

		public void ShowTab(OutputTab tab)
		{
			this.SelectedTab = (Int32)tab;
			this.ShowRequested?.Invoke(this, EventArgs.Empty);
		}
	}
}