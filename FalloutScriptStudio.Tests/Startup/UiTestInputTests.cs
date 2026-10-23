namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.Tests.Accessibility;
	using FalloutScriptStudio.Tests.Support;
	using FalloutScriptStudio.UiTesting;
	using ICSharpCode.AvalonEdit;
	using Xunit;

	/// <summary>The keys and the text the UI tests send to the editor of the program (it has no keyboard focus while the tests run).</summary>
	public sealed class UiTestInputTests
	{
		private static TextEditor Show(String text, out Window window)
		{
			TextEditor editor = new TextEditor { Text = text };
			window = OffscreenWindow.Show(editor);
			WindowHost.Pump();
			return editor;
		}

		[Fact]
		public void Typed_text_is_inserted_at_the_caret_character_by_character()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				TextEditor editor = Show("abc", out Window window);
				editor.CaretOffset = 1;

				UiTestInput.Run(window, UiTestInput.TextPrefix + "x");
				UiTestInput.Run(window, UiTestInput.TextPrefix + "y");

				Assert.Equal("axybc", editor.Text);
				window.Close();
			});
		}

		[Fact]
		public void A_key_goes_through_the_handlers_of_the_editor()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				TextEditor editor = Show("one" + Environment.NewLine + "two", out Window window);
				editor.CaretOffset = 1;

				UiTestInput.Run(window, UiTestInput.KeyPrefix + "Down");
				Assert.Equal(2, editor.TextArea.Caret.Line);

				UiTestInput.Run(window, UiTestInput.KeyPrefix + "Return");
				Assert.Equal("one" + Environment.NewLine + "t" + Environment.NewLine + "wo", editor.Text);
				window.Close();
			});
		}

		[Fact]
		public void A_command_that_is_not_understood_changes_nothing()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				TextEditor editor = Show("abc", out Window window);

				UiTestInput.Run(window, "key:NoSuchKey");
				UiTestInput.Run(window, "nonsense");

				Assert.Equal("abc", editor.Text);
				window.Close();
			});
		}
	}
}