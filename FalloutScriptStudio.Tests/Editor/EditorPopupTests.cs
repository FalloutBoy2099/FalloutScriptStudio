namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Support;
	using ICSharpCode.AvalonEdit;
	using Xunit;

	/// <summary>The completion list, the hover tip and the code tip are real top-level windows: the tests build them but must never open them.</summary>
	public sealed class EditorPopupTests
	{
		private const String Script = "procedure other;\r\n\r\nprocedure other begin\r\n   ot\r\nend\r\n";

		[Fact]
		public void Completion_GoesThroughThePresenter_AndTheWindowIsNeverShown()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("   ot", StringComparison.Ordinal) + 5;

				Assert.True(session.Completion.Show(true));

				Assert.True(session.Completion.IsOpen);
				Assert.Same(session.Completion.Window, Assert.Single(env.Popups.CompletionWindows));
				Assert.False(session.Completion.Window!.IsVisible);
				Assert.NotEmpty(session.Completion.Window.CompletionList.CompletionData);

				session.Completion.Close();
				Assert.False(session.Completion.IsOpen);
			});
		}

		[Fact]
		public void HoverTip_GoesThroughThePresenter_AndTheToolTipIsNeverOpened()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("#include \"a.h\"\r\n\r\nprocedure start begin\r\nend\r\n");
				document.ParserErrors = new List<BuildError>
				{
					new BuildError(BuildErrorType.Parser, "message", document.FilePath!, 1),
				};
				using EditorSession session = env.StartSession(document);

				Boolean shown = session.Tips.ShowHoverTip(2, new Point(5, 5));

				Assert.True(shown);
				Assert.False(Assert.Single(env.Popups.ToolTips).IsOpen);
			});
		}

		[Fact]
		public void CodeTip_GoesThroughThePresenter_AndThePopupIsNeverOpened()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				TextEditor editor = new TextEditor();
				Window window = OffscreenWindow.Show(editor, 600, 400);
				UiThread.Flush();
				using EditorSession session = new EditorSession(editor, env.CreateDocument(Script), env.Services, () => { });
				session.Start();
				UiThread.Flush();

				session.Tips.ShowHint("Press TAB", 5, TimeSpan.FromSeconds(5));

				Assert.True(session.Tips.IsCodeTipVisible);
				Assert.False(Assert.Single(env.Popups.Popups).IsOpen);

				session.Tips.HideCodeTip();
				Assert.False(session.Tips.IsCodeTipVisible);
				window.Close();
			});
		}
	}
}