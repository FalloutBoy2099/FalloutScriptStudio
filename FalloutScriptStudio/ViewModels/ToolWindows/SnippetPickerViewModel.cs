namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>
	/// View model of the list of snippets: "Insert Snippet" (every snippet in effect) and "Surround With" (the snippets that use the selected
	/// text), filtered while the user types.
	/// </summary>
	public sealed partial class SnippetPickerViewModel : ToolWindowViewModelBase
	{
		private SnippetPickerRequest _request = new(Array.Empty<Snippet>(), false);

		[ObservableProperty]
		private String _filter = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private SnippetListItem? _selectedItem;

		/// <summary>Gets the snippets that match the filter, the best match first.</summary>
		public ObservableCollection<SnippetListItem> Items { get; } = [];

		/// <summary>Gets the title of the window.</summary>
		public String Title => Loc.Get(this._request.Surround ? "Str.ToolWindows.Snippets.SurroundTitle" : "Str.ToolWindows.Snippets.InsertTitle");

		/// <summary>Gets the text of the button that chooses the snippet.</summary>
		public String AcceptText => Loc.Get(this._request.Surround ? "Str.ToolWindows.Snippets.Surround" : "Str.ToolWindows.Snippets.Insert");

		/// <summary>Gets the line under the list: the number of snippets shown.</summary>
		public String StatusText => this.Items.Count == 0 && this._request.Snippets.Count > 0
			? Loc.Get("Str.ToolWindows.Snippets.NoMatch")
			: Loc.Format("Str.ToolWindows.Snippets.Count", this.Items.Count, this._request.Snippets.Count);

		/// <summary>Gets the snippet the user chose, or <see langword="null"/> when the list was cancelled.</summary>
		public Snippet? ChosenSnippet { get; private set; }

		/// <summary>Prepares the list.</summary>
		/// <param name="request">What to show.</param>
		public void Initialize(SnippetPickerRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			this._request = request;
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.AcceptText));
			this.Filter = String.Empty;
			this.Fill(false);
		}

		/// <summary>Moves the selection by a number of rows (the arrow keys while the filter box has the focus).</summary>
		/// <param name="delta">The number of rows; negative moves up.</param>
		public void MoveSelection(Int32 delta)
		{
			if (this.Items.Count == 0)
			{
				return;
			}

			Int32 index = this.SelectedItem is null ? -1 : this.Items.IndexOf(this.SelectedItem);
			this.SelectedItem = this.Items[Math.Clamp(index + delta, 0, this.Items.Count - 1)];
		}

		/// <summary>Chooses the selected snippet and closes the window.</summary>
		[RelayCommand(CanExecute = nameof(HasSelection))]
		public void Accept()
		{
			if (this.SelectedItem is null)
			{
				return;
			}

			this.ChosenSnippet = this.SelectedItem.Snippet;
			this.RequestClose(true);
		}

		/// <summary>Closes the window without choosing.</summary>
		[RelayCommand]
		public void Cancel()
		{
			this.ChosenSnippet = null;
			this.RequestClose(false);
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.AcceptText));
			this.Fill(true);
		}

		partial void OnFilterChanged(String value)
		{
			this.Fill(false);
		}

		private Boolean HasSelection()
		{
			return this.SelectedItem is not null;
		}

		private void Fill(Boolean keepSelection)
		{
			Snippet? previous = keepSelection ? this.SelectedItem?.Snippet : null;
			IEnumerable<SnippetListItem> all = this._request.Snippets.Select(snippet => new SnippetListItem(snippet));
			IReadOnlyList<SnippetListItem> shown = this.Filter.Trim().Length == 0
				? all.ToList()
				: Rank(all, this.Filter.Trim());

			this.Items.Clear();
			foreach (SnippetListItem item in shown)
			{
				this.Items.Add(item);
			}

			this.SelectedItem = (previous is null ? null : this.Items.FirstOrDefault(item => Object.ReferenceEquals(item.Snippet, previous))) ?? this.Items.FirstOrDefault();
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		/// <summary>Keeps the snippets whose trigger or description matches the text, a match on the trigger first.</summary>
		private static List<SnippetListItem> Rank(IEnumerable<SnippetListItem> items, String pattern)
		{
			List<(SnippetListItem Item, Int32 Score, Int32 Index)> scored = [];
			Int32 index = 0;
			foreach (SnippetListItem item in items)
			{
				Int32? byTrigger = FuzzyMatcher.Score(pattern, item.Trigger);
				Int32? byDescription = FuzzyMatcher.Score(pattern, item.Description);
				if (byTrigger is not null || byDescription is not null)
				{
					// A match on the trigger beats one on the description of the same kind.
					Int32 score = Math.Max(byTrigger is null ? 0 : byTrigger.Value + 50, byDescription ?? 0);
					scored.Add((item, score, index));
				}

				index++;
			}

			scored.Sort(static (a, b) =>
			{
				Int32 byScore = b.Score.CompareTo(a.Score);
				return byScore != 0 ? byScore : a.Index.CompareTo(b.Index);
			});
			return scored.Select(entry => entry.Item).ToList();
		}
	}
}