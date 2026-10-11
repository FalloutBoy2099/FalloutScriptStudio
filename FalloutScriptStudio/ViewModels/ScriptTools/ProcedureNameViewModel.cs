namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;

	/// <summary>
	/// View model of the procedure name dialog (legacy <c>ProcForm</c>): validates the name while it is typed.
	/// </summary>
	public sealed partial class ProcedureNameViewModel : ScriptToolViewModelBase
	{
		private ProcedureNameRequest _request = new ProcedureNameRequest();

		/// <summary>Gets or sets the window title.</summary>
		[ObservableProperty]
		public partial String Title { get; set; } = Loc.Get("Str.ScriptTools.ProcTitleCreate");

		/// <summary>Gets or sets the caption of the confirm button.</summary>
		[ObservableProperty]
		public partial String OkText { get; set; } = Loc.Get("Str.ScriptTools.ProcOkCreate");

		/// <summary>Gets or sets the text of the name field.</summary>
		[ObservableProperty]
		public partial String Text { get; set; } = String.Empty;

		/// <summary>Gets a value indicating whether the name field cannot be edited.</summary>
		[ObservableProperty]
		public partial Boolean NameReadOnly { get; private set; }

		/// <summary>Gets the explanation shown under the name field.</summary>
		[ObservableProperty]
		public partial String Hint { get; private set; } = String.Empty;

		/// <summary>Gets the reason the current text is rejected; empty when it is valid or nothing was typed.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasError))]
		public partial String ErrorMessage { get; private set; } = String.Empty;

		/// <summary>Gets a value indicating whether the current text can be accepted.</summary>
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
		public partial Boolean IsValid { get; private set; }

		/// <summary>Gets a value indicating whether the insertion options are shown.</summary>
		[ObservableProperty]
		public partial Boolean ShowOptions { get; private set; }

		/// <summary>Gets a value indicating whether the body of the current procedure can be copied.</summary>
		[ObservableProperty]
		public partial Boolean CanCopyBody { get; private set; }

		/// <summary>Gets or sets a value indicating whether the body of the current procedure is copied into the new one.</summary>
		[ObservableProperty]
		public partial Boolean CopyBody { get; set; }

		/// <summary>Gets or sets a value indicating whether the procedure is inserted at the end of the script.</summary>
		[ObservableProperty]
		public partial Boolean PlaceAtEnd { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the procedure is inserted after the current procedure.</summary>
		[ObservableProperty]
		public partial Boolean PlaceAfterCurrent { get; set; }

		/// <summary>Gets a value indicating whether <see cref="ErrorMessage"/> is shown.</summary>
		public Boolean HasError => this.ErrorMessage.Length > 0;

		/// <summary>Gets the hint shown in the empty name field.</summary>
		public String PlaceholderText => this._request.Mode == ProcedureNameDialogMode.Rename ? Loc.Get("Str.ScriptTools.ProcPlaceholderNewName") : ProcedureNameValidator.ExampleName;

		/// <summary>Gets the result the user accepted, or <see langword="null"/> when the dialog was cancelled.</summary>
		public ProcedureNameResult? Result { get; private set; }

		/// <summary>
		/// Prepares the dialog.
		/// </summary>
		/// <param name="request">What the dialog is used for.</param>
		public void Initialize(ProcedureNameRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			this._request = request;
			this.OnPropertyChanged(nameof(this.PlaceholderText));
			Boolean rename = request.Mode == ProcedureNameDialogMode.Rename;
			this.Title = rename
				? (request.Subject.Length > 0 ? Loc.Format("Str.ScriptTools.ProcTitleRenameSubject", request.Subject) : Loc.Get("Str.ScriptTools.ProcTitleRename"))
				: Loc.Get("Str.ScriptTools.ProcTitleCreate");
			this.OkText = rename ? Loc.Get("Str.Common.Rename") : Loc.Get("Str.ScriptTools.ProcOkCreate");
			this.NameReadOnly = request.NameReadOnly;
			this.Hint = Loc.Get(rename ? "Str.ScriptTools.ProcHintRename" : "Str.ScriptTools.ProcHintCreate");
			this.ShowOptions = !rename && request.CanChoosePlacement;
			this.CanCopyBody = this.ShowOptions && request.CanCopyBody;
			this.CopyBody = false;
			this.PlaceAfterCurrent = request.PreferInsertAfter;
			this.PlaceAtEnd = !request.PreferInsertAfter;
			this.Text = rename ? request.InitialName.Trim() : ProcedureNameValidator.PrepareInitialName(request.InitialName, request.Mode);
			this.Revalidate();
		}

		partial void OnTextChanged(String value)
		{
			this.Revalidate();
		}

		partial void OnPlaceAtEndChanged(Boolean value)
		{
			this.PlaceAfterCurrent = !value;
		}

		partial void OnPlaceAfterCurrentChanged(Boolean value)
		{
			this.PlaceAtEnd = !value;
		}

		private void Revalidate()
		{
			IReadOnlyCollection<String> existing = this._request.ExistingNames;
			ProcedureNameValidation validation = ProcedureNameValidator.Validate(this.Text, this._request.Mode, existing, this._request.InitialName);
			this.IsValid = validation.IsValid;
			this.ErrorMessage = validation.Error ?? String.Empty;
		}

		[RelayCommand(CanExecute = nameof(IsValid))]
		private void Accept()
		{
			ProcedureNameValidation validation = ProcedureNameValidator.Validate(this.Text, this._request.Mode, this._request.ExistingNames, this._request.InitialName);
			if (!validation.IsValid)
			{
				return;
			}

			ProcedurePlacement placement = this.ShowOptions
				? (this.PlaceAfterCurrent ? ProcedurePlacement.AfterCurrent : ProcedurePlacement.AtEnd)
				: ProcedurePlacement.AtCaret;
			this.Result = new ProcedureNameResult(validation.Name, validation.Declaration, placement, this.CanCopyBody && this.CopyBody);
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
