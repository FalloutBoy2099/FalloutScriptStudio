namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Xunit;

	/// <summary>The search field is a single line: what the selection and the clipboard may put into it.</summary>
	public sealed class SearchTextSuggestionTests
	{
		[Theory]
		[InlineData("first line\r\nsecond line", "first line")]
		[InlineData("first line\nsecond line", "first line")]
		[InlineData("   padded   ", "padded")]
		[InlineData("\r\n\r\n  after blank lines  \r\nnext", "after blank lines")]
		[InlineData("", "")]
		[InlineData("   \r\n  ", "")]
		public void FirstLine_KeepsOnlyTheFirstNonBlankLine(String selection, String expected)
		{
			Assert.Equal(expected, SearchTextSuggestion.FirstLine(selection));
		}

		[Fact]
		public void FirstLine_IgnoresALineLongerThanTheLimit()
		{
			Assert.Equal(new String('x', SearchTextSuggestion.MaxLength), SearchTextSuggestion.FirstLine(new String('x', SearchTextSuggestion.MaxLength) + "\r\nnext"));
			Assert.Equal(String.Empty, SearchTextSuggestion.FirstLine(new String('x', SearchTextSuggestion.MaxLength + 1) + "\r\nnext"));
			Assert.Equal(String.Empty, SearchTextSuggestion.FirstLine(null));
		}

		[Theory]
		[InlineData("one line", "one line")]
		[InlineData("  one line\r\n", "one line")]
		[InlineData("two\r\nlines", "")]
		[InlineData("two\nlines", "")]
		[InlineData("   ", "")]
		public void SingleLine_RefusesAMultiLineClipboard(String clipboard, String expected)
		{
			Assert.Equal(expected, SearchTextSuggestion.SingleLine(clipboard));
		}

		[Fact]
		public void SingleLine_RefusesALongClipboard()
		{
			Assert.Equal(String.Empty, SearchTextSuggestion.SingleLine(new String('y', SearchTextSuggestion.MaxLength + 1)));
			Assert.Equal(new String('y', SearchTextSuggestion.MaxLength), SearchTextSuggestion.SingleLine(new String('y', SearchTextSuggestion.MaxLength)));
		}

		[Fact]
		public void Choose_PrefersTheCallerThenTheSelectionThenTheOpenWindowThenThePreviousSearch()
		{
			Func<String> clipboard = () => "clip";

			Assert.Equal("given", SearchTextSuggestion.Choose("given", "sel", "open", "last", clipboard));
			Assert.Equal("sel", SearchTextSuggestion.Choose(null, "sel", "open", "last", clipboard));
			Assert.Equal("open", SearchTextSuggestion.Choose(" ", "", "open", "last", clipboard));
			Assert.Equal("last", SearchTextSuggestion.Choose(null, null, null, "last", clipboard));
			Assert.Equal("clip", SearchTextSuggestion.Choose(null, null, null, null, clipboard));
		}

		[Fact]
		public void Choose_UsesTheFirstLineOfAMultiLineSelection()
		{
			String selection = "procedure start begin\r\n\tdisplay_msg(\"x\");\r\nend";

			Assert.Equal("procedure start begin", SearchTextSuggestion.Choose(null, selection, null, null, () => "clip"));
		}

		[Fact]
		public void Choose_IgnoresAMultiLineOrLongClipboard()
		{
			Assert.Equal(String.Empty, SearchTextSuggestion.Choose(null, null, null, null, () => "a\r\nb"));
			Assert.Equal(String.Empty, SearchTextSuggestion.Choose(null, null, null, null, () => new String('z', 300)));
		}

		[Fact]
		public void Choose_DoesNotReadTheClipboardWhenSomethingElseIsUsable()
		{
			Boolean read = false;

			SearchTextSuggestion.Choose("given", null, null, null, () =>
			{
				read = true;
				return "clip";
			});

			Assert.False(read);
		}

		[Fact]
		public void TheSearchField_NeverHoldsALineBreak()
		{
			StaTestRunner.Run(() =>
			{
				using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
				AdvancedSearchViewModel viewModel = new AdvancedSearchViewModel(new FakeWorkspace().Interface, new FakeSettingsService(temp.Path), new FakeDialogService(), new ScriptSearcher());

				viewModel.SetSearchText("first\r\nsecond\r\nthird");
				Assert.Equal("first", viewModel.SearchText);

				viewModel.SearchText = "pasted\nlines";
				Assert.Equal("pasted", viewModel.SearchText);

				viewModel.SearchText = "\r\n  after a blank line\r\nmore";
				Assert.Equal("after a blank line", viewModel.SearchText);

				viewModel.SearchText = "single";
				Assert.Equal("single", viewModel.SearchText);
				return Task.CompletedTask;
			});
		}
	}
}