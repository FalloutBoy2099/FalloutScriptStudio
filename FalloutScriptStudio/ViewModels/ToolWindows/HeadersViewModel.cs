namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// View model of the headers picker: lists the <c>.h</c> files of the headers folder and filters them while the user types.
	/// </summary>
	public sealed partial class HeadersViewModel : ToolWindowViewModelBase
	{
		private List<HeaderFileItem> _allItems = new List<HeaderFileItem>();

		[ObservableProperty]
		private String _filter = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private HeaderFileItem? _selectedItem;

		[ObservableProperty]
		private String _statusText = String.Empty;

		/// <summary>Gets the header files that match the current filter.</summary>
		public ObservableCollection<HeaderFileItem> Items { get; } = new ObservableCollection<HeaderFileItem>();

		/// <summary>Gets the path of the header file the user chose, or <see langword="null"/> when the picker was cancelled.</summary>
		public String? SelectedPath { get; private set; }

		/// <summary>
		/// Gets or sets the action run for a header chosen while Ctrl is held; the window stays open afterwards. Without it Ctrl is
		/// ignored and every choice closes the window.
		/// </summary>
		public Func<String, Task>? KeepOpenHandler { get; set; }

		/// <summary>
		/// Chooses the selected header: closes the window, or, with <paramref name="keepOpen"/> (Ctrl held) and a
		/// <see cref="KeepOpenHandler"/>, hands the header to the handler and leaves the window open.
		/// </summary>
		/// <param name="keepOpen">Whether the window is to stay open.</param>
		/// <returns>A task that completes when the choice was handled.</returns>
		public async Task ChooseAsync(Boolean keepOpen)
		{
			if (this.SelectedItem is null)
			{
				return;
			}

			if (keepOpen && this.KeepOpenHandler is { } handler)
			{
				await handler(this.SelectedItem.FullPath).ConfigureAwait(true);
				return;
			}

			this.Accept();
		}

		/// <summary>
		/// Finds the header files below a folder and shows them.
		/// </summary>
		/// <param name="headersDirectory">The headers folder.</param>
		/// <returns>A task that completes when the list is filled.</returns>
		public async Task LoadAsync(String headersDirectory)
		{
			ArgumentException.ThrowIfNullOrEmpty(headersDirectory);

			List<HeaderFileItem> items = await Task.Run(() => FindHeaders(headersDirectory)).ConfigureAwait(true);
			this._allItems = items;
			this.ApplyFilter();
		}

		/// <summary>
		/// Finds the header files below a folder.
		/// </summary>
		/// <param name="headersDirectory">The headers folder.</param>
		/// <returns>The files, ordered by name.</returns>
		public static List<HeaderFileItem> FindHeaders(String headersDirectory)
		{
			ArgumentException.ThrowIfNullOrEmpty(headersDirectory);

			if (!Directory.Exists(headersDirectory))
			{
				return new List<HeaderFileItem>();
			}

			EnumerationOptions options = new EnumerationOptions
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = true,
				MatchCasing = MatchCasing.CaseInsensitive,
			};

			return Directory.EnumerateFiles(headersDirectory, "*.h", options)
				.Select(file => new HeaderFileItem(
					FormatDisplayName(Path.GetFileName(file)),
					file,
					Path.GetRelativePath(headersDirectory, file)))
				.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
				.ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		/// <summary>
		/// Formats a file name the way the list shows it: lower case with a capital first letter.
		/// </summary>
		/// <param name="fileName">The file name.</param>
		/// <returns>The display name.</returns>
		public static String FormatDisplayName(String fileName)
		{
			if (String.IsNullOrEmpty(fileName))
			{
				return String.Empty;
			}

			String lower = fileName.ToLowerInvariant();
			return String.Concat(Char.ToUpperInvariant(lower[0]).ToString(), lower.AsSpan(1));
		}

		/// <summary>
		/// Determines whether a header matches a filter text (case-insensitive, matches the file name or the relative path).
		/// </summary>
		/// <param name="item">The header.</param>
		/// <param name="filter">The filter text; everything matches when empty.</param>
		/// <returns><see langword="true"/> when the header matches.</returns>
		public static Boolean Matches(HeaderFileItem item, String? filter)
		{
			ArgumentNullException.ThrowIfNull(item);

			String text = (filter ?? String.Empty).Trim();
			return text.Length == 0
				|| item.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
				|| item.RelativePath.Contains(text, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Moves the selection by a number of rows (used for the arrow keys while the filter box has the focus).</summary>
		/// <param name="delta">The number of rows; negative moves up.</param>
		public void MoveSelection(Int32 delta)
		{
			if (this.Items.Count == 0)
			{
				return;
			}

			Int32 index = this.SelectedItem is null ? -1 : this.Items.IndexOf(this.SelectedItem);
			Int32 target = Math.Clamp(index + delta, 0, this.Items.Count - 1);
			this.SelectedItem = this.Items[target];
		}

		partial void OnFilterChanged(String value)
		{
			this.ApplyFilter();
		}

		private void ApplyFilter()
		{
			HeaderFileItem? previous = this.SelectedItem;

			this.Items.Clear();
			foreach (HeaderFileItem item in this._allItems.Where(item => Matches(item, this.Filter)))
			{
				this.Items.Add(item);
			}

			this.SelectedItem = previous is not null && this.Items.Contains(previous) ? previous : this.Items.FirstOrDefault();
			this.StatusText = this._allItems.Count == 0
				? Loc.Get("Str.ToolWindows.Headers.NoneFound")
				: Loc.Format("Str.ToolWindows.Headers.Count", this.Items.Count, this._allItems.Count);
		}

		[RelayCommand(CanExecute = nameof(HasSelection))]
		private void Accept()
		{
			if (this.SelectedItem is null)
			{
				return;
			}

			this.SelectedPath = this.SelectedItem.FullPath;
			this.RequestClose(true);
		}

		[RelayCommand]
		private void Cancel()
		{
			this.SelectedPath = null;
			this.RequestClose(false);
		}

		private Boolean HasSelection()
		{
			return this.SelectedItem is not null;
		}
	}
}
