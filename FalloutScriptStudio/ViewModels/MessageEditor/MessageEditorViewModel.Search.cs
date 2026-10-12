namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;

	public sealed partial class MessageEditorViewModel
	{
		private String _searchText = String.Empty;
		private Boolean _isFilterActive;
		private Int32 _matchCount;
		private Boolean _anyRowMarkedAsMatch;
		private String _searchStatus = String.Empty;

		/// <summary>Gets or sets the text to search for; matching ignores the letter case and looks at every shown column.</summary>
		public String SearchText
		{
			get => this._searchText;
			set
			{
				if (this.SetProperty(ref this._searchText, value ?? String.Empty))
				{
					this.ClearPinnedRows();
					this.UpdateSearchMatches();
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether the grid shows only the rows that contain the search text.</summary>
		public Boolean IsFilterActive
		{
			get => this._isFilterActive;
			set
			{
				if (this.SetProperty(ref this._isFilterActive, value))
				{
					this.ClearPinnedRows();
					this.RowsView.Refresh();
					this.UpdateSearchMatches();
				}
			}
		}

		/// <summary>Gets the number of rows that contain the search text.</summary>
		public Int32 MatchCount
		{
			get => this._matchCount;
			private set => this.SetProperty(ref this._matchCount, value);
		}

		/// <summary>Gets a short description of the search result for the status bar.</summary>
		public String SearchStatus
		{
			get => this._searchStatus;
			private set => this.SetProperty(ref this._searchStatus, value);
		}

		/// <summary>Finds the next cell that contains the search text, wrapping around at the end.</summary>
		[RelayCommand]
		private void FindNext()
		{
			this.Find(true);
		}

		/// <summary>Finds the previous cell that contains the search text, wrapping around at the start.</summary>
		[RelayCommand]
		private void FindPrevious()
		{
			this.Find(false);
		}

		/// <summary>Clears the search text.</summary>
		[RelayCommand]
		private void ClearSearch()
		{
			this.SearchText = String.Empty;
		}

		/// <summary>Selects the next row with a structure problem, wrapping around at the end.</summary>
		[RelayCommand]
		private void GoToNextIssue()
		{
			this.GoToIssue(true);
		}

		/// <summary>Selects the previous row with a structure problem, wrapping around at the start.</summary>
		[RelayCommand]
		private void GoToPreviousIssue()
		{
			this.GoToIssue(false);
		}

		/// <summary>
		/// Recounts the rows that contain the search text and refreshes the filter. Called after every change of the rows.
		/// </summary>
		internal void UpdateSearchMatches()
		{
			String needle = this._searchText.Trim();
			Int32 count = 0;

			// Without a search text no row matches; when no row is marked either, there is nothing to walk (every edit comes through here).
			if (needle.Length > 0 || this._anyRowMarkedAsMatch)
			{
				foreach (MessageRowViewModel row in this._rows)
				{
					Boolean match = row.Matches(needle, this._showLipColumn);
					row.IsSearchMatch = match;
					if (match)
					{
						count++;
					}
				}

				this._anyRowMarkedAsMatch = count > 0;
			}

			this.MatchCount = count;
			this.SearchStatus = needle.Length == 0
				? String.Empty
				: Loc.Format(count == 1 ? "Str.MessageEditor.Status.MatchOne" : "Str.MessageEditor.Status.MatchMany", count);
			if (this._isFilterActive)
			{
				this.RowsView.Refresh();
			}
		}

		private static Boolean CellContains(MessageRowViewModel row, MessageGridColumn column, String needle)
		{
			Boolean applicable = column == MessageGridColumn.Text || row.IsMessage;
			return applicable && row.GetCellText(column).Contains(needle, StringComparison.OrdinalIgnoreCase);
		}

		private Boolean FilterRow(Object item)
		{
			return item is MessageRowViewModel row && this.PassesFilter(row);
		}

		private Boolean PassesFilter(MessageRowViewModel row)
		{
			return !this._isFilterActive || this._searchText.Trim().Length == 0 || row.IsSearchMatch || row.IsPinnedVisible;
		}

		private void ClearPinnedRows()
		{
			foreach (MessageRowViewModel row in this._rows)
			{
				row.IsPinnedVisible = false;
			}
		}

		private void Find(Boolean forward)
		{
			String needle = this._searchText.Trim();
			if (needle.Length == 0)
			{
				this.StatusMessage = Loc.Get("Str.MessageEditor.Status.EnterSearch");
				this._platform.Beep(MessageEditorBeep.NotFound);
				return;
			}

			List<MessageRowViewModel> rows = this._rows.Where(this.PassesFilter).ToList();
			List<MessageGridColumn> columns = new List<MessageGridColumn> { MessageGridColumn.Line, MessageGridColumn.Text, MessageGridColumn.Comment };
			if (this._showLipColumn)
			{
				columns.Add(MessageGridColumn.Sound);
			}

			Int32 total = rows.Count * columns.Count;
			if (total > 0)
			{
				Int32 rowIndex = this.SelectedRow is null ? -1 : rows.IndexOf(this.SelectedRow);
				Int32 columnIndex = Math.Max(0, columns.IndexOf(this.CurrentColumn));
				Int32 start = rowIndex < 0 ? (forward ? -1 : total) : (rowIndex * columns.Count) + columnIndex;
				for (Int32 step = 1; step <= total; step++)
				{
					Int32 position = forward ? start + step : start - step;
					Boolean wrapped = position < 0 || position >= total;
					position = ((position % total) + total) % total;
					MessageRowViewModel row = rows[position / columns.Count];
					MessageGridColumn column = columns[position % columns.Count];
					if (CellContains(row, column, needle))
					{
						this.RequestFocus(row, column, false);
						this.StatusMessage = wrapped ? Loc.Get("Str.MessageEditor.Status.Wrapped") : String.Empty;
						return;
					}
				}
			}

			this._platform.Beep(MessageEditorBeep.NotFound);
			this.StatusMessage = Loc.Get("Str.MessageEditor.Status.NotFound");
		}

		private void GoToIssue(Boolean forward)
		{
			this.FlushStructureCheck();
			Int32 count = this._rows.Count;
			Int32 start = this.GetCurrentIndex();
			for (Int32 step = 1; step <= count; step++)
			{
				Int32 index = (((forward ? start + step : start - step) % count) + count) % count;
				if (this._rows[index].HasIssue)
				{
					this.RequestFocus(this._rows[index], this.CurrentColumn, false);
					this.StatusMessage = this._rows[index].IssueText.Replace(Environment.NewLine, " ", StringComparison.Ordinal);
					return;
				}
			}

			this.StatusMessage = Loc.Get("Str.MessageEditor.Status.NoProblemsFound");
		}
	}
}
