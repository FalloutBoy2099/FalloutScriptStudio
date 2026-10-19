namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using Commands;
	using Core.TextTools;
	using Localization;
	using Services.ToolWindows;

	/// <summary>
	/// View model of the command palette: every command of the application in one list, the recently used ones first, filtered while the user
	/// types (a name that starts with the text, then a word that starts with it, then letters in order; case and accents are ignored).
	/// A command that cannot run now stays in the list, dimmed, with the reason, and cannot be chosen.
	/// </summary>
	public sealed partial class CommandPaletteViewModel : ToolWindowViewModelBase
	{
		private CommandPaletteRequest _request = new() { Entries = Array.Empty<CommandEntry>() };
		private List<CommandPaletteItem> _all = [];
		private Dictionary<String, Int32> _recentRank = new(StringComparer.Ordinal);

		[ObservableProperty]
		private String _filter = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private CommandPaletteItem? _selectedItem;

		/// <summary>Gets the commands that match the filter, the best match first.</summary>
		public ObservableCollection<CommandPaletteItem> Items { get; } = [];

		/// <summary>Gets the title of the window.</summary>
		public String Title => Loc.Get("Str.Shell.Palette.Title");

		/// <summary>Gets the text of the filter box (its placeholder and the name of the box for a screen reader).</summary>
		public String SearchLabel => Loc.Get("Str.Shell.Palette.Search");

		/// <summary>
		/// Gets the line under the list, announced by a screen reader when it changes: the number of commands, and why the selected command
		/// cannot run when it cannot.
		/// </summary>
		public String StatusText
		{
			get
			{
				String count = this.Items.Count == 0 ? Loc.Get("Str.Shell.Palette.NoMatch") : Loc.Format("Str.Shell.Palette.Count", this.Items.Count);
				return this.SelectedItem is { IsEnabled: false } selected
					? count + " " + Loc.Format("Str.Shell.Palette.SelectedUnavailable", selected.Name, selected.ReasonText)
					: count;
			}
		}

		/// <summary>Gets the command the user chose, or <see langword="null"/> when the palette was cancelled.</summary>
		public CommandEntry? ChosenEntry { get; private set; }

		/// <summary>Prepares the list.</summary>
		/// <param name="request">What to show.</param>
		public void Initialize(CommandPaletteRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			this._request = request;
			this.Filter = String.Empty;
			this.Rebuild();
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

		/// <summary>Chooses the selected command and closes the palette; the command runs after the palette is closed.</summary>
		[RelayCommand(CanExecute = nameof(CanAccept))]
		public void Accept()
		{
			if (this.SelectedItem is not { IsEnabled: true } item)
			{
				return;
			}

			this.ChosenEntry = item.Entry;
			this.RequestClose(true);
		}

		/// <summary>Closes the palette without choosing.</summary>
		[RelayCommand]
		public void Cancel()
		{
			this.ChosenEntry = null;
			this.RequestClose(false);
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.SearchLabel));
			this.Rebuild();
		}

		partial void OnFilterChanged(String value)
		{
			this.Refresh(false);
		}

		partial void OnSelectedItemChanged(CommandPaletteItem? value)
		{
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		private Boolean CanAccept()
		{
			return this.SelectedItem is { IsEnabled: true };
		}

		private void Rebuild()
		{
			this._recentRank = new(StringComparer.Ordinal);
			Int32 rank = 0;
			foreach (String id in this._request.RecentIds.Reverse())
			{
				this._recentRank.TryAdd(id, rank++);
			}

			this._all = this._request.Entries
				.Select(entry => CommandPaletteItem.Create(entry, this._request.Gestures, this._recentRank.ContainsKey(entry.Id)))
				.ToList();
			this.Refresh(true);
		}

		private void Refresh(Boolean keepSelection)
		{
			String pattern = this.Filter.Trim();
			CommandEntry? previous = keepSelection ? this.SelectedItem?.Entry : null;
			List<CommandPaletteItem> ordered = pattern.Length == 0 ? this.OrderWithoutFilter() : this.OrderByScore(pattern);

			this.Items.Clear();
			foreach (CommandPaletteItem item in ordered)
			{
				item.Highlight(pattern);
				this.Items.Add(item);
			}

			this.SelectedItem = (previous is null ? null : this.Items.FirstOrDefault(item => ReferenceEquals(item.Entry, previous))) ?? this.Items.FirstOrDefault();
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		private List<CommandPaletteItem> OrderWithoutFilter()
		{
			return this._all
				.Select((item, index) => (Item: item, Index: index))
				.OrderBy(pair => this._recentRank.TryGetValue(pair.Item.Entry.Id, out Int32 rank) ? rank : Int32.MaxValue)
				.ThenBy(pair => pair.Index)
				.Select(pair => pair.Item)
				.ToList();
		}

		private List<CommandPaletteItem> OrderByScore(String pattern)
		{
			List<(CommandPaletteItem Item, Int32 Score, Int32 Recent, Int32 Index)> scored = [];
			for (Int32 index = 0; index < this._all.Count; index++)
			{
				CommandPaletteItem item = this._all[index];
				Int32? byName = FuzzyMatcher.Score(pattern, item.Name);
				Int32? byPath = FuzzyMatcher.Score(pattern, item.FullName);
				Int32? best = byName is null ? byPath : byPath is null ? byName : Math.Max(byName.Value, byPath.Value);
				if (best is null)
				{
					continue;
				}

				scored.Add((item, best.Value, this._recentRank.TryGetValue(item.Entry.Id, out Int32 rank) ? rank : Int32.MaxValue, index));
			}

			// A better kind of match wins (a name that starts with the text before a word that starts with it...); inside the same kind the
			// commands used last come first, then the closer match, then the order of the menus.
			scored.Sort(static (a, b) =>
			{
				Int32 byTier = FuzzyMatcher.GetTier(b.Score).CompareTo(FuzzyMatcher.GetTier(a.Score));
				if (byTier != 0)
				{
					return byTier;
				}

				Int32 byRecent = a.Recent.CompareTo(b.Recent);
				if (byRecent != 0)
				{
					return byRecent;
				}

				Int32 byScore = b.Score.CompareTo(a.Score);
				return byScore != 0 ? byScore : a.Index.CompareTo(b.Index);
			});
			return scored.Select(entry => entry.Item).ToList();
		}
	}
}