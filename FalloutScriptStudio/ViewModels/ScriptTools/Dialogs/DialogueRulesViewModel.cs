namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;

	/// <summary>
	/// View model of the dialogue function rules window (legacy "Functions Config" dialog): the list of user functions and macros
	/// the dialogue parser and the flowchart treat as replies, options, messages or calls.
	/// </summary>
	public sealed partial class DialogueRulesViewModel : DialogueDialogViewModelBase
	{
		private readonly IDialogueRulesService _rules;
		private readonly IDialogService _dialogs;
		private String _loadedSnapshot = String.Empty;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueRulesViewModel"/> class.
		/// </summary>
		/// <param name="rules">Loads and saves the user's rules.</param>
		/// <param name="dialogs">Shows the questions and the errors.</param>
		public DialogueRulesViewModel(IDialogueRulesService rules, IDialogService dialogs)
		{
			ArgumentNullException.ThrowIfNull(rules);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._rules = rules;
			this._dialogs = dialogs;
		}

		/// <summary>Gets the user's rules, in the order they are saved.</summary>
		public ObservableCollection<DialogueRuleItemViewModel> Rules { get; } = [];

		/// <summary>Gets or sets the selected row.</summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveRuleCommand))]
		public partial DialogueRuleItemViewModel? SelectedRule { get; set; }

		/// <summary>Gets a value indicating whether the rules are being loaded or saved.</summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
		public partial Boolean IsBusy { get; private set; }

		/// <summary>Gets the first problem of the rules, or <see langword="null"/> when all rules are valid.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasError))]
		public partial String? ErrorMessage { get; private set; }

		/// <summary>Gets a value indicating whether a rule has a problem.</summary>
		public Boolean HasError => this.ErrorMessage is not null;

		/// <summary>Gets a value indicating whether the rules differ from the ones that were loaded.</summary>
		public Boolean IsModified => !String.Equals(this.CreateSnapshot(), this._loadedSnapshot, StringComparison.Ordinal);

		/// <summary>Gets a value indicating whether the rules were written to the rules file.</summary>
		public Boolean IsSaved { get; private set; }

		/// <summary>Gets a text that lists the built-in functions, which are always recognized and cannot be edited.</summary>
		public String BuiltInSummary => Loc.Format("Str.ScriptTools.RulesBuiltIn", String.Join(", ", DialogueFunctionRules.BuiltIn.Select(rule => rule.Name)));

		/// <summary>Loads the user's rules into the grid.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the rules are shown.</returns>
		public async Task LoadAsync(CancellationToken cancellationToken = default)
		{
			this.IsBusy = true;
			try
			{
				DialogueFunctionRules loaded = await this._rules.LoadAsync(cancellationToken).ConfigureAwait(true);
				this.SetRules(loaded.UserRules);
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>Replaces the rows with the given rules and takes them as the unmodified state.</summary>
		/// <param name="userRules">The rules.</param>
		public void SetRules(IEnumerable<DialogueFunctionRule> userRules)
		{
			ArgumentNullException.ThrowIfNull(userRules);

			foreach (DialogueRuleItemViewModel item in this.Rules)
			{
				item.PropertyChanged -= this.OnItemPropertyChanged;
			}

			this.Rules.Clear();
			foreach (DialogueFunctionRule rule in userRules.Where(rule => !rule.IsDefault))
			{
				this.AttachItem(DialogueRuleItemViewModel.FromRule(rule));
			}

			this.SelectedRule = this.Rules.FirstOrDefault();
			this._loadedSnapshot = this.CreateSnapshot();
			this.Revalidate();
		}

		[RelayCommand]
		private void AddRule()
		{
			DialogueRuleItemViewModel item = new()
			{ Kind = "Option", TotalArgs = 3, MessageArg = 1, NodeArg = 2, IqArg = 3 };
			this.AttachItem(item);
			this.SelectedRule = item;
			this.Revalidate();
		}

		[RelayCommand(CanExecute = nameof(CanRemoveRule))]
		private void RemoveRule()
		{
			DialogueRuleItemViewModel? selected = this.SelectedRule;
			if (selected is null)
			{
				return;
			}

			Int32 index = this.Rules.IndexOf(selected);
			selected.PropertyChanged -= this.OnItemPropertyChanged;
			this.Rules.Remove(selected);
			this.SelectedRule = this.Rules.Count == 0 ? null : this.Rules[Math.Min(index, this.Rules.Count - 1)];
			this.Revalidate();
		}

		private Boolean CanRemoveRule()
		{
			return this.SelectedRule is not null;
		}

		[RelayCommand(CanExecute = nameof(CanSave))]
		private async Task SaveAsync()
		{
			this.Revalidate();
			if (this.HasError)
			{
				return;
			}

			if (!this.IsModified)
			{
				this.RequestClose(false);
				return;
			}

			this.IsBusy = true;
			try
			{
				await this._rules.SaveAsync(this.Rules.Select(item => item.ToRule()).ToList()).ConfigureAwait(true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this.IsBusy = false;
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.ScriptTools.RulesTitle"), Loc.Format("Str.ScriptTools.RulesErrSave", ex.Message)).ConfigureAwait(true);
				return;
			}

			this.IsBusy = false;
			this.IsSaved = true;
			this.RequestClose(true);
		}

		private Boolean CanSave()
		{
			return !this.IsBusy && !this.HasError;
		}

		[RelayCommand]
		private async Task CancelAsync()
		{
			if (this.IsModified && !await this._dialogs.ConfirmAsync(Loc.Get("Str.ScriptTools.RulesTitle"), Loc.Get("Str.ScriptTools.RulesAskDiscard"), Loc.Get("Str.ScriptTools.RulesDiscard"), Loc.Get("Str.ScriptTools.RulesKeepEditing")).ConfigureAwait(true))
			{
				return;
			}

			this.RequestClose(false);
		}

		private void AttachItem(DialogueRuleItemViewModel item)
		{
			item.PropertyChanged += this.OnItemPropertyChanged;
			this.Rules.Add(item);
		}

		private void OnItemPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(DialogueRuleItemViewModel.Error) or nameof(DialogueRuleItemViewModel.HasError))
			{
				return;
			}

			this.Revalidate();
		}

		/// <summary>Checks every row, including the names against each other, and updates <see cref="ErrorMessage"/>.</summary>
		private void Revalidate()
		{
			HashSet<String> names = new(StringComparer.OrdinalIgnoreCase);
			String? first = null;
			for (Int32 index = 0; index < this.Rules.Count; index++)
			{
				DialogueRuleItemViewModel item = this.Rules[index];
				String? error = item.ValidateRow();
				if (error is null && !names.Add(item.Name.Trim()))
				{
					item.MarkDuplicate();
					error = item.Error;
				}

				if (error is not null && first is null)
				{
					first = Loc.Format("Str.ScriptTools.RulesRowError", index + 1, error);
				}
			}

			this.ErrorMessage = first;
			this.SaveCommand.NotifyCanExecuteChanged();
			this.OnPropertyChanged(nameof(this.IsModified));
		}

		private String CreateSnapshot()
		{
			return String.Join("\n", this.Rules.Select(item => item.CreateKey()));
		}
	}
}