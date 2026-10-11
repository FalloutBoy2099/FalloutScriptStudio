namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Xunit;

	public sealed class ProcedureNameViewModelTests
	{
		private static ProcedureNameViewModel Create(ProcedureNameRequest request)
		{
			ProcedureNameViewModel viewModel = new ProcedureNameViewModel();
			viewModel.Initialize(request);
			return viewModel;
		}

		[Fact]
		public void Initialize_Create_StartsEmptyAndInvalidWithoutAMessage()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest());

			Assert.Equal("Create procedure", viewModel.Title);
			Assert.Equal(String.Empty, viewModel.Text);
			Assert.False(viewModel.IsValid);
			Assert.False(viewModel.HasError);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
			Assert.Equal(ProcedureNameValidator.ExampleName, viewModel.PlaceholderText);
		}

		[Fact]
		public void Initialize_Create_IncrementsTheSuggestedNumber()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { InitialName = "node2" });

			Assert.Equal("node3", viewModel.Text);
			Assert.True(viewModel.IsValid);
		}

		[Fact]
		public void Initialize_FromWord_KeepsTheWord()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { InitialName = "node2", Mode = ProcedureNameDialogMode.CreateFromWord });

			Assert.Equal("node2", viewModel.Text);
		}

		[Fact]
		public void Typing_ValidatesLive()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { ExistingNames = new[] { "start" } });

			viewModel.Text = "start";
			Boolean duplicateValid = viewModel.IsValid;
			String duplicateMessage = viewModel.ErrorMessage;
			viewModel.Text = "begin";
			String reservedMessage = viewModel.ErrorMessage;
			viewModel.Text = "fine(a, b)";

			Assert.False(duplicateValid);
			Assert.Contains("already exists", duplicateMessage, StringComparison.Ordinal);
			Assert.Contains("reserved", reservedMessage, StringComparison.Ordinal);
			Assert.True(viewModel.IsValid);
			Assert.False(viewModel.HasError);
			Assert.True(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void Accept_ReturnsTheDeclarationWithVariableKeywords()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest());
			Boolean? accepted = null;
			viewModel.CloseRequested += (_, value) => accepted = value;
			viewModel.Text = "Foo(a, b)";

			viewModel.AcceptCommand.Execute(null);

			Assert.True(accepted);
			Assert.NotNull(viewModel.Result);
			Assert.Equal("Foo", viewModel.Result.Name);
			Assert.Equal("Foo(variable a, variable b)", viewModel.Result.Declaration);
			Assert.Equal(ProcedurePlacement.AtCaret, viewModel.Result.Placement);
			Assert.False(viewModel.Result.CopyBody);
		}

		[Fact]
		public void Accept_WithOptions_ReportsPlacementAndCopy()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { CanChoosePlacement = true, CanCopyBody = true, PreferInsertAfter = true });
			viewModel.Text = "Foo";
			viewModel.CopyBody = true;

			viewModel.AcceptCommand.Execute(null);

			Assert.True(viewModel.ShowOptions);
			Assert.True(viewModel.PlaceAfterCurrent);
			Assert.False(viewModel.PlaceAtEnd);
			Assert.Equal(ProcedurePlacement.AfterCurrent, viewModel.Result!.Placement);
			Assert.True(viewModel.Result.CopyBody);
		}

		[Fact]
		public void Options_PlaceAtEndIsTheDefaultAndTheTwoChoicesExcludeEachOther()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { CanChoosePlacement = true });
			viewModel.Text = "Foo";

			Assert.True(viewModel.PlaceAtEnd);
			viewModel.PlaceAfterCurrent = true;
			Assert.False(viewModel.PlaceAtEnd);
			viewModel.PlaceAtEnd = true;
			Assert.False(viewModel.PlaceAfterCurrent);
			viewModel.AcceptCommand.Execute(null);

			Assert.Equal(ProcedurePlacement.AtEnd, viewModel.Result!.Placement);
			Assert.False(viewModel.CanCopyBody);
		}

		[Fact]
		public void Accept_CopyIsIgnoredWhenTheCurrentProcedureCannotBeCopied()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { CanChoosePlacement = true, CanCopyBody = false });
			viewModel.Text = "Foo";
			viewModel.CopyBody = true;

			viewModel.AcceptCommand.Execute(null);

			Assert.False(viewModel.Result!.CopyBody);
		}

		[Fact]
		public void Cancel_ClosesWithoutAResult()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest());
			Boolean? accepted = null;
			viewModel.CloseRequested += (_, value) => accepted = value;
			viewModel.Text = "Foo";

			viewModel.CancelCommand.Execute(null);

			Assert.False(accepted);
			Assert.Null(viewModel.Result);
		}

		[Fact]
		public void Rename_UsesTheSubjectInTheTitleAndKeepsTheOriginalName()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest
			{
				InitialName = "start",
				Mode = ProcedureNameDialogMode.Rename,
				Subject = "Procedure",
				ExistingNames = new[] { "start", "other" },
				CanChoosePlacement = true,
			});

			Assert.Equal("Rename Procedure", viewModel.Title);
			Assert.Equal("Rename", viewModel.OkText);
			Assert.Equal("start", viewModel.Text);
			Assert.True(viewModel.IsValid);
			Assert.False(viewModel.ShowOptions);

			viewModel.Text = "other";
			Assert.False(viewModel.IsValid);
			viewModel.Text = "renamed";
			viewModel.AcceptCommand.Execute(null);

			Assert.Equal("renamed", viewModel.Result!.Declaration);
		}

		[Fact]
		public void ReadOnlyName_IsExposed()
		{
			ProcedureNameViewModel viewModel = Create(new ProcedureNameRequest { InitialName = "look_at_p_proc", Mode = ProcedureNameDialogMode.CreateFromWord, NameReadOnly = true });

			Assert.True(viewModel.NameReadOnly);
			Assert.Equal("look_at_p_proc", viewModel.Text);
			Assert.True(viewModel.IsValid);
		}
	}
}
