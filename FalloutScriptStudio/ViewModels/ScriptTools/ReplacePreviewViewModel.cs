namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Replace;

	/// <summary>
	/// View model of the preview of "Replace in Files": a tree of the files with their replacements, each with its line before and after, a
	/// check box per file and per replacement (all ticked at first), and a summary of what "Replace Selected" will do.
	/// </summary>
	public sealed partial class ReplacePreviewViewModel : ScriptToolViewModelBase
	{
		private readonly List<ReplaceNodeViewModel> _matchNodes = [];
		private ReplacePlan _plan = new(Array.Empty<ReplaceFilePlan>(), Array.Empty<ReplaceSkip>(), 0, false, System.Text.Encoding.UTF8);
		private Int32 _selectedCount;
		private Boolean _bulk;

		/// <summary>Initializes a new instance of the <see cref="ReplacePreviewViewModel"/> class.</summary>
		public ReplacePreviewViewModel()
		{
			_ = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.OnLanguageChanged());
		}

		/// <summary>Gets the title of the window.</summary>
		public String Title => Loc.Get("Str.ScriptTools.ReplTitle");

		/// <summary>Gets the text that is looked for.</summary>
		[ObservableProperty]
		public partial String FindText { get; private set; } = String.Empty;

		/// <summary>Gets the replacement.</summary>
		[ObservableProperty]
		public partial String ReplaceText { get; private set; } = String.Empty;

		/// <summary>Gets the options of the search as a sentence ("Match case, regular expression").</summary>
		[ObservableProperty]
		public partial String OptionsText { get; private set; } = String.Empty;

		/// <summary>Gets the files of the tree.</summary>
		public IReadOnlyList<ReplaceNodeViewModel> Files { get; private set; } = Array.Empty<ReplaceNodeViewModel>();

		/// <summary>Gets the number of replacements in the preview.</summary>
		public Int32 TotalCount => this._matchNodes.Count;

		/// <summary>Gets the number of ticked replacements.</summary>
		public Int32 SelectedCount => this._selectedCount;

		/// <summary>Gets the number of files that have a ticked replacement.</summary>
		public Int32 SelectedFileCount => this.Files.Count(file => file.IsChecked != false);

		/// <summary>Gets the line "42 replacements in 7 files": what Replace Selected does now.</summary>
		public String Summary
		{
			get
			{
				Int32 files = this.SelectedFileCount;
				String key = this._selectedCount == 0
					? "Str.ScriptTools.ReplSummaryNone"
					: this._selectedCount == 1 ? "Str.ScriptTools.ReplSummaryOne"
					: files == 1 ? "Str.ScriptTools.ReplSummaryOneFile"
					: "Str.ScriptTools.ReplSummaryMany";
				return Loc.Format(key, this._selectedCount, files, this._matchNodes.Count);
			}
		}

		/// <summary>Gets a value indicating whether the preview lists only the first replacements.</summary>
		public Boolean IsTruncated => this._plan.Truncated;

		/// <summary>Gets the text that tells the preview is limited.</summary>
		public String TruncatedText => Loc.Format("Str.ScriptTools.ReplTruncated", this._matchNodes.Count);

		/// <summary>Gets the files that have matches but are left out, with the reason, one text per file.</summary>
		public IReadOnlyList<String> SkippedItems => this._plan.Skipped
			.Select(skip => Loc.Format(skip.Reason == ReplaceSkipReason.ReadOnly ? "Str.ScriptTools.ReplSkippedReadOnly" : "Str.ScriptTools.ReplSkippedUnreadable", skip.Path, skip.Matches))
			.ToList();

		/// <summary>Gets a value indicating whether some files are left out.</summary>
		public Boolean HasSkipped => this._plan.Skipped.Count > 0;

		/// <summary>Gets the caption of the list of the files that are left out.</summary>
		public String SkippedText => Loc.Format("Str.ScriptTools.ReplSkipped", this._plan.Skipped.Count);

		/// <summary>Gets the replacements the user ticked per file, or <see langword="null"/> when the preview was cancelled.</summary>
		public IReadOnlyList<ReplaceSelection>? Selection { get; private set; }

		/// <summary>Builds the tree from a plan.</summary>
		/// <param name="plan">The replacements that would be made.</param>
		/// <param name="request">What was asked.</param>
		public void Initialize(ReplacePlan plan, ReplaceInFilesRequest request)
		{
			ArgumentNullException.ThrowIfNull(plan);
			ArgumentNullException.ThrowIfNull(request);

			foreach (ReplaceNodeViewModel node in this._matchNodes)
			{
				node.PropertyChanged -= this.OnMatchPropertyChanged;
			}

			this._matchNodes.Clear();
			this._plan = plan;
			this.FindText = request.Matcher.Options.Text;
			this.ReplaceText = request.Replacement;
			this.OptionsText = DescribeOptions(request);

			List<ReplaceNodeViewModel> files = new(plan.Files.Count);
			foreach (ReplaceFilePlan file in plan.Files)
			{
				ReplaceNodeViewModel node = ReplaceNodeViewModel.CreateFile(file);
				foreach (ReplaceNodeViewModel match in node.EnumerateMatches())
				{
					match.PropertyChanged += this.OnMatchPropertyChanged;
					this._matchNodes.Add(match);
				}

				files.Add(node);
			}

			this.Files = files;
			this._selectedCount = this._matchNodes.Count;
			this.OnPropertyChanged(nameof(this.Files));
			this.OnPropertyChanged(nameof(this.TotalCount));
			this.OnPropertyChanged(nameof(this.IsTruncated));
			this.OnPropertyChanged(nameof(this.TruncatedText));
			this.OnPropertyChanged(nameof(this.SkippedItems));
			this.OnPropertyChanged(nameof(this.HasSkipped));
			this.OnPropertyChanged(nameof(this.SkippedText));
			this.UpdateSelection();
		}

		[RelayCommand]
		private void SelectAll()
		{
			this.SetAll(true);
		}

		[RelayCommand]
		private void DeselectAll()
		{
			this.SetAll(false);
		}

		[RelayCommand]
		private void ExpandAll()
		{
			foreach (ReplaceNodeViewModel file in this.Files)
			{
				file.IsExpanded = true;
			}
		}

		[RelayCommand]
		private void CollapseAll()
		{
			foreach (ReplaceNodeViewModel file in this.Files)
			{
				file.IsExpanded = false;
			}
		}

		[RelayCommand(CanExecute = nameof(CanApply))]
		private void Apply()
		{
			List<ReplaceSelection> selection = [];
			foreach (ReplaceNodeViewModel file in this.Files)
			{
				List<ReplaceMatch> ticked = file.Children.Where(child => child.IsChecked == true).Select(child => child.Match!).ToList();
				if (ticked.Count > 0)
				{
					selection.Add(new ReplaceSelection(file.File, ticked));
				}
			}

			this.Selection = selection;
			this.RequestClose(true);
		}

		private Boolean CanApply()
		{
			return this._selectedCount > 0;
		}

		[RelayCommand]
		private void Cancel()
		{
			this.Selection = null;
			this.RequestClose(false);
		}

		private static String DescribeOptions(ReplaceInFilesRequest request)
		{
			List<String> options = [];
			if (request.Matcher.Options.MatchCase)
			{
				options.Add(Loc.Get("Str.ScriptTools.ReplOptMatchCase"));
			}

			if (request.Matcher.Options.WholeWord)
			{
				options.Add(Loc.Get("Str.ScriptTools.ReplOptWholeWord"));
			}

			if (request.Matcher.Options.UseRegex)
			{
				options.Add(Loc.Get("Str.ScriptTools.ReplOptRegex"));
			}

			return String.Join(", ", options);
		}

		private void SetAll(Boolean value)
		{
			// Ticking thousands of replacements one by one would update the summary once for each; it is counted again at the end.
			this._bulk = true;
			try
			{
				foreach (ReplaceNodeViewModel file in this.Files)
				{
					file.IsChecked = value;
				}
			}
			finally
			{
				this._bulk = false;
			}

			this._selectedCount = this._matchNodes.Count(node => node.IsChecked == true);
			this.UpdateSelection();
		}

		private void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.Summary));
			this.OnPropertyChanged(nameof(this.TruncatedText));
			this.OnPropertyChanged(nameof(this.SkippedItems));
			this.OnPropertyChanged(nameof(this.SkippedText));
			foreach (ReplaceNodeViewModel file in this.Files)
			{
				file.RefreshTexts();
			}
		}

		private void OnMatchPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (this._bulk || e.PropertyName != nameof(ReplaceNodeViewModel.IsChecked) || sender is not ReplaceNodeViewModel node)
			{
				return;
			}

			this._selectedCount += node.IsChecked == true ? 1 : -1;
			this.UpdateSelection();
		}

		private void UpdateSelection()
		{
			this.OnPropertyChanged(nameof(this.SelectedCount));
			this.OnPropertyChanged(nameof(this.SelectedFileCount));
			this.OnPropertyChanged(nameof(this.Summary));
			this.ApplyCommand.NotifyCanExecuteChanged();
		}
	}
}