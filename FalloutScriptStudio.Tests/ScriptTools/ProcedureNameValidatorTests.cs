namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using Xunit;

	public sealed class ProcedureNameValidatorTests
	{
		private static readonly String[] Existing = { "start", "Talk_p_proc" };

		[Theory]
		[InlineData("MyProc")]
		[InlineData("_private")]
		[InlineData("proc_2")]
		public void Validate_PlainIdentifier_IsValid(String text)
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate(text, ProcedureNameDialogMode.Create, Existing);

			Assert.True(result.IsValid);
			Assert.Null(result.Error);
			Assert.Equal(text, result.Name);
			Assert.Equal(text, result.Declaration);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		public void Validate_Empty_IsInvalidWithoutMessage(String text)
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate(text, ProcedureNameDialogMode.Create, Existing);

			Assert.False(result.IsValid);
			Assert.Null(result.Error);
		}

		[Theory]
		[InlineData("my proc")]
		[InlineData("my-proc")]
		[InlineData("proc!")]
		[InlineData("1proc")]
		public void Validate_BadCharacters_IsInvalid(String text)
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate(text, ProcedureNameDialogMode.Create, Existing);

			Assert.False(result.IsValid);
			Assert.False(String.IsNullOrEmpty(result.Error));
		}

		[Theory]
		[InlineData("begin")]
		[InlineData("PROCEDURE")]
		[InlineData("Variable")]
		[InlineData("end")]
		public void Validate_ReservedWord_IsInvalid(String text)
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate(text, ProcedureNameDialogMode.Create, Existing);

			Assert.False(result.IsValid);
			Assert.Contains("reserved word", result.Error);
		}

		[Theory]
		[InlineData("start")]
		[InlineData("START")]
		[InlineData("talk_P_proc")]
		public void Validate_DuplicateNameIsCaseInsensitive(String text)
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate(text, ProcedureNameDialogMode.Create, Existing);

			Assert.False(result.IsValid);
			Assert.Contains("already exists", result.Error);
		}

		[Fact]
		public void Validate_Rename_AllowsKeepingTheOriginalNameEvenInDifferentCase()
		{
			ProcedureNameValidation same = ProcedureNameValidator.Validate("start", ProcedureNameDialogMode.Rename, Existing, "start");
			ProcedureNameValidation changedCase = ProcedureNameValidator.Validate("Start", ProcedureNameDialogMode.Rename, Existing, "start");
			ProcedureNameValidation other = ProcedureNameValidator.Validate("Talk_p_proc", ProcedureNameDialogMode.Rename, Existing, "start");

			Assert.True(same.IsValid);
			Assert.True(changedCase.IsValid);
			Assert.False(other.IsValid);
			Assert.Contains("already in use", other.Error);
		}

		[Fact]
		public void Validate_Rename_RejectsArgumentList()
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate("foo(a)", ProcedureNameDialogMode.Rename, Existing, "foo");

			Assert.False(result.IsValid);
		}

		[Fact]
		public void Validate_Arguments_GetTheVariableKeyword()
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate("Foo(a, b ,variable c)", ProcedureNameDialogMode.Create, Existing);

			Assert.True(result.IsValid);
			Assert.Equal("Foo", result.Name);
			Assert.Equal("Foo(variable a, variable b, variable c)", result.Declaration);
		}

		[Fact]
		public void Validate_EmptyArgumentList_IsKeptAsTyped()
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate("Foo()", ProcedureNameDialogMode.CreateFromWord, Existing);

			Assert.True(result.IsValid);
			Assert.Equal("Foo()", result.Declaration);
		}

		[Theory]
		[InlineData("Foo(a")]
		[InlineData("Foo(a))")]
		[InlineData("Foo((a))")]
		[InlineData("Foo(a,,b)")]
		[InlineData("Foo(a,)")]
		[InlineData("Foo(a b)")]
		[InlineData("Foo(a, a)")]
		[InlineData("Foo(1a)")]
		[InlineData("Foo(begin)")]
		[InlineData("Foo(a) x")]
		[InlineData("(a)")]
		public void Validate_BadArguments_IsInvalid(String text)
		{
			ProcedureNameValidation result = ProcedureNameValidator.Validate(text, ProcedureNameDialogMode.Create, Existing);

			Assert.False(result.IsValid);
		}

		[Theory]
		[InlineData("proc", "proc")]
		[InlineData("proc1", "proc2")]
		[InlineData("proc09", "proc10")]
		[InlineData("proc007", "proc008")]
		[InlineData("proc99", "proc100")]
		[InlineData("123", "124")]
		public void IncrementTrailingNumber_KeepsTheWidth(String name, String expected)
		{
			Assert.Equal(expected, ProcedureNameValidator.IncrementTrailingNumber(name));
		}

		[Fact]
		public void PrepareInitialName_CreateIncrementsTheNumberOfTheNameBeforeTheArguments()
		{
			Assert.Equal("dialog_node_2(a,b)", ProcedureNameValidator.PrepareInitialName("dialog_node_1(a,b)", ProcedureNameDialogMode.Create));
			Assert.Equal("Node2", ProcedureNameValidator.PrepareInitialName("Node1", ProcedureNameDialogMode.Create));
		}

		[Fact]
		public void PrepareInitialName_FromWordKeepsTheName()
		{
			Assert.Equal("Node1", ProcedureNameValidator.PrepareInitialName("Node1", ProcedureNameDialogMode.CreateFromWord));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("two words")]
		[InlineData("begin")]
		[InlineData("variable")]
		public void PrepareInitialName_UnusableCandidates_GiveNothing(String? candidate)
		{
			Assert.Equal(String.Empty, ProcedureNameValidator.PrepareInitialName(candidate, ProcedureNameDialogMode.Create));
		}

		[Theory]
		[InlineData("foo", true)]
		[InlineData("_foo1", true)]
		[InlineData("1foo", false)]
		[InlineData("foo bar", false)]
		[InlineData("", false)]
		public void IsIdentifier_Works(String text, Boolean expected)
		{
			Assert.Equal(expected, ProcedureNameValidator.IsIdentifier(text));
		}
	}
}