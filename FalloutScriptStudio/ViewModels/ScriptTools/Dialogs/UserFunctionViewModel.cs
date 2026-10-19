namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// View model of the add / edit user function dialog (legacy <c>UserFunction</c> form): the name, the short description and the code
	/// of a function of the functions tree.
	/// </summary>
	public sealed partial class UserFunctionViewModel : DialogueDialogViewModelBase
	{
		/// <summary>Gets the title of the window.</summary>
		[ObservableProperty]
		public partial String Title { get; private set; } = Loc.Get("Str.ScriptTools.UfTitleAdd");

		/// <summary>Gets the text of the accept button.</summary>
		[ObservableProperty]
		public partial String OkText { get; private set; } = Loc.Get("Str.Common.Add");

		/// <summary>Gets a value indicating whether a new function is being added (and not an existing one edited).</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanHighlight))]
		public partial Boolean IsAdding { get; private set; } = true;

		/// <summary>Gets or sets the name shown in the functions tree.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ErrorMessage))]
		[NotifyPropertyChangedFor(nameof(HasError))]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		public partial String FunctionName { get; set; } = String.Empty;

		/// <summary>Gets or sets the short description (the tool tip of the tree node).</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ErrorMessage))]
		[NotifyPropertyChangedFor(nameof(HasError))]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		public partial String Description { get; set; } = String.Empty;

		/// <summary>Gets or sets the code inserted into the script; it can span several lines.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Keyword))]
		[NotifyPropertyChangedFor(nameof(CanHighlight))]
		[NotifyPropertyChangedFor(nameof(HighlightText))]
		[NotifyPropertyChangedFor(nameof(ErrorMessage))]
		[NotifyPropertyChangedFor(nameof(HasError))]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		public partial String Code { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the keyword of the code is added to the syntax highlighting (legacy "Add to Highlight").</summary>
		[ObservableProperty]
		public partial Boolean AddToHighlight { get; set; }

		/// <summary>Gets the word the code starts with, which would be highlighted.</summary>
		public String Keyword => FunctionKeyword.Extract(this.Code);

		/// <summary>Gets a value indicating whether the highlight option can be used: only for a new function whose code starts with a word.</summary>
		public Boolean CanHighlight => this.IsAdding && this.Keyword.Length > 0;

		/// <summary>Gets the caption of the highlight option.</summary>
		public String HighlightText => this.Keyword.Length > 0 ? Loc.Format("Str.ScriptTools.UfHighlight", this.Keyword) : Loc.Get("Str.ScriptTools.UfHighlightGeneric");

		/// <summary>Gets the problem with the entered values, or <see langword="null"/> when there is none to report yet.</summary>
		/// <remarks>Missing values are not reported (the accept button stays disabled), only values that cannot be stored.</remarks>
		public String? ErrorMessage => this.FindError(reportMissing: false);

		/// <summary>Gets a value indicating whether <see cref="ErrorMessage"/> has a text.</summary>
		public Boolean HasError => this.ErrorMessage is not null;

		/// <summary>Gets a value indicating whether the entered values can be accepted.</summary>
		public Boolean IsValid => this.FindError(reportMissing: true) is null;

		/// <summary>Gets the definition the user entered, or <see langword="null"/> when the dialog was cancelled.</summary>
		public FunctionDefinition? Result { get; private set; }

		/// <summary>Prepares the dialog.</summary>
		/// <param name="existing">The function to edit, or <see langword="null"/> to add a new one.</param>
		/// <param name="addToHighlight">The initial state of the highlight option (it is remembered between two uses).</param>
		public void Initialize(FunctionDefinition? existing, Boolean addToHighlight = false)
		{
			this.IsAdding = existing is null;
			this.Title = Loc.Get(this.IsAdding ? "Str.ScriptTools.UfTitleAdd" : "Str.ScriptTools.UfTitleEdit");
			this.OkText = Loc.Get(this.IsAdding ? "Str.Common.Add" : "Str.Common.Save");
			this.FunctionName = existing?.Name ?? String.Empty;
			this.Description = existing?.Description ?? String.Empty;
			this.Code = existing?.Code ?? String.Empty;
			this.AddToHighlight = this.IsAdding && addToHighlight;
		}

		/// <summary>Builds the definition from the entered values: the name, description and code are trimmed and the description is put on one line.</summary>
		/// <returns>The definition.</returns>
		public FunctionDefinition CreateDefinition()
		{
			return new FunctionDefinition(this.FunctionName.Trim(), CollapseLines(this.Description), this.Code.Trim());
		}

		private static String CollapseLines(String text)
		{
			return text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Trim();
		}

		private String? FindError(Boolean reportMissing)
		{
			if (this.FunctionName.Trim().Length == 0)
			{
				return reportMissing ? Loc.Get("Str.ScriptTools.RulesErrNameEmpty") : null;
			}

			if (!this.CreateDefinition().TryValidate(out String? error))
			{
				return error;
			}

			if (this.Code.Contains(FunctionCatalogParser.LineBreakMarker, StringComparison.Ordinal))
			{
				return Loc.Format("Str.ScriptTools.UfErrLineBreakMarker", FunctionCatalogParser.LineBreakMarker);
			}

			if (this.Code.Trim().Length == 0)
			{
				return reportMissing ? Loc.Get("Str.ScriptTools.UfErrCodeEmpty") : null;
			}

			return null;
		}

		[RelayCommand(CanExecute = nameof(IsValid))]
		private void Accept()
		{
			if (!this.IsValid)
			{
				return;
			}

			this.Result = this.CreateDefinition();
			this.RequestClose(true);
		}

		[RelayCommand]
		private void Cancel()
		{
			this.Result = null;
			this.RequestClose(false);
		}
	}
}