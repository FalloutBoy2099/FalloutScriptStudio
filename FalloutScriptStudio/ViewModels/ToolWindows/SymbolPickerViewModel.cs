namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>
	/// View model of the symbol list: "Go to symbol" (the procedures, macros and variables of the whole project, filtered while the user
	/// types) and the choice between several definitions of a name.
	/// </summary>
	public sealed partial class SymbolPickerViewModel : ToolWindowViewModelBase, IDisposable
	{
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private SymbolPickerRequest _request = new SymbolPickerRequest();
		private IProjectSymbolIndex? _subscribedIndex;
		private Boolean _disposed;

		[ObservableProperty]
		private String _filter = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		private SymbolListItem? _selectedItem;

		/// <summary>Gets the symbols that match the filter, the best match first.</summary>
		public ObservableCollection<SymbolListItem> Items { get; } = new ObservableCollection<SymbolListItem>();

		/// <summary>Gets a value indicating whether the list has a filter box (the search, not the choice between definitions).</summary>
		public Boolean IsFilterVisible => this._request.Mode == SymbolPickerMode.Search;

		/// <summary>Gets a value indicating whether the text above the list is shown (the choice between definitions, which has no filter box).</summary>
		public Boolean IsPromptVisible => this._request.Mode == SymbolPickerMode.Choose;

		/// <summary>Gets the title of the window.</summary>
		public String Title => Loc.Get(this._request.Mode == SymbolPickerMode.Search ? "Str.ToolWindows.Symbols.Title" : "Str.ToolWindows.Symbols.ChooseTitle");

		/// <summary>Gets the text above the list (the name of the list for a screen reader).</summary>
		public String Prompt => this._request.Mode == SymbolPickerMode.Search
			? Loc.Get("Str.ToolWindows.Symbols.Search")
			: Loc.Format("Str.ToolWindows.Symbols.ChoosePrompt", this._request.Name);

		/// <summary>Gets the line under the list: the number of symbols shown, or the progress of the indexing.</summary>
		public String StatusText
		{
			get
			{
				if (this._request.Mode == SymbolPickerMode.Choose)
				{
					return Loc.Format("Str.ToolWindows.Symbols.Locations", this.Items.Count);
				}

				IProjectSymbolIndex? index = this._request.Index;
				if (index is null)
				{
					return String.Empty;
				}

				ProjectIndexProgress progress = index.Progress;
				if (progress.Phase == ProjectIndexPhase.Indexing && progress.Total > 0)
				{
					return Loc.Format("Str.ToolWindows.Symbols.Indexing", progress.Done, progress.Total);
				}

				if (progress.Phase == ProjectIndexPhase.Scanning)
				{
					return Loc.Get("Str.ToolWindows.Symbols.Scanning");
				}

				return index.SymbolCount == 0
					? Loc.Get("Str.ToolWindows.Symbols.NoneIndexed")
					: Loc.Format("Str.ToolWindows.Symbols.Count", this.Items.Count, index.SymbolCount);
			}
		}

		/// <summary>Gets the symbol the user chose, or <see langword="null"/> when the list was cancelled.</summary>
		public IndexedSymbol? SelectedSymbol { get; private set; }

		/// <summary>Prepares the list.</summary>
		/// <param name="request">What to show.</param>
		public void Initialize(SymbolPickerRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			this.Unsubscribe();
			this._request = request;
			if (request.Mode == SymbolPickerMode.Search && request.Index is { } index)
			{
				index.Updated += this.OnIndexChanged;
				index.ProgressChanged += this.OnIndexProgress;
				this._subscribedIndex = index;
			}

			this.OnPropertyChanged(nameof(this.IsFilterVisible));
			this.OnPropertyChanged(nameof(this.IsPromptVisible));
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.Prompt));
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

		/// <summary>Chooses the selected symbol and closes the window.</summary>
		[RelayCommand(CanExecute = nameof(HasSelection))]
		public void Accept()
		{
			if (this.SelectedItem is null)
			{
				return;
			}

			this.SelectedSymbol = this.SelectedItem.Symbol;
			this.RequestClose(true);
		}

		/// <summary>Closes the window without choosing.</summary>
		[RelayCommand]
		public void Cancel()
		{
			this.SelectedSymbol = null;
			this.RequestClose(false);
		}

		/// <summary>Stops following the index.</summary>
		public void Dispose()
		{
			this._disposed = true;
			this.Unsubscribe();
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.Prompt));
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
			IndexedSymbol? previous = keepSelection ? this.SelectedItem?.Symbol : null;
			IReadOnlyList<IndexedSymbol> symbols = this._request.Mode == SymbolPickerMode.Search
				? this._request.Index?.Search(this.Filter, this._request.MaxResults) ?? Array.Empty<IndexedSymbol>()
				: this._request.Candidates;

			this.Items.Clear();
			foreach (IndexedSymbol symbol in symbols)
			{
				this.Items.Add(new SymbolListItem(symbol, this._request.RootFolder));
			}

			this.SelectedItem = (previous is null ? null : this.Items.FirstOrDefault(item => Object.ReferenceEquals(item.Symbol, previous))) ?? this.Items.FirstOrDefault();
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		private void OnIndexChanged(Object? sender, EventArgs e)
		{
			this.Post(() => this.Fill(true));
		}

		private void OnIndexProgress(Object? sender, ProjectIndexProgress progress)
		{
			this.Post(() => this.OnPropertyChanged(nameof(this.StatusText)));
		}

		private void Post(Action action)
		{
			void Run()
			{
				if (!this._disposed)
				{
					action();
				}
			}

			if (this._context is null)
			{
				Run();
			}
			else
			{
				this._context.Post(static state => ((Action)state!)(), (Action)Run);
			}
		}

		private void Unsubscribe()
		{
			if (this._subscribedIndex is { } index)
			{
				index.Updated -= this.OnIndexChanged;
				index.ProgressChanged -= this.OnIndexProgress;
				this._subscribedIndex = null;
			}
		}
	}
}
