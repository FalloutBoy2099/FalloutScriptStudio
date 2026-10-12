namespace FalloutScriptStudio.Tests.Editor
{
	using FalloutScriptStudio.Tests.Support;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Windows;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Folding;
	using ICSharpCode.AvalonEdit.Rendering;
	using Xunit;

	/// <summary>
	/// Typing in an unfolded procedure must lay out the edited line only, not every visible line of the procedure (AvalonEdit redraws the
	/// whole folding section that contains an edit), while folded procedures keep being redrawn.
	/// </summary>
	public sealed class FoldingRedrawTests
	{
		private static String CreateScript()
		{
			StringBuilder text = new StringBuilder();
			text.Append("procedure start;\r\nprocedure other;\r\n\r\nprocedure start begin\r\n");
			for (Int32 i = 0; i < 60; i++)
			{
				text.Append("   display_msg(\"line ").Append(i).Append("\");\r\n");
			}

			text.Append("end\r\n\r\nprocedure other begin\r\n   display_msg(\"other\");\r\nend\r\n");
			return text.ToString();
		}

		/// <summary>A script editor in an off-screen window, so that the text view has a size and lays out lines.</summary>
		private sealed class Host : IDisposable
		{
			private readonly Window _window;
			private readonly EditorTestEnvironment _env = new EditorTestEnvironment();

			public Host()
			{
				this.Document = this._env.CreateDocument(CreateScript());
				FalloutScriptStudio.Views.Editor.ScriptEditorView view = new FalloutScriptStudio.Views.Editor.ScriptEditorView(this._env.Services) { DataContext = this.Document };
				this._window = OffscreenWindow.Show(view, 900, 900);
				UiThread.Flush();
				this.Session = view.Session!;
				Assert.True(this.Session.Folding.Count >= 2);
			}

			public DocumentViewModel Document { get; }

			public EditorSession Session { get; }

			public void Dispose()
			{
				this._window.Content = null;
				this._window.Close();
				UiThread.Flush();
				this._env.Dispose();
			}
		}

		private static List<VisualLine> Lines(EditorSession session)
		{
			session.Context.TextView.EnsureVisualLines();
			return session.Context.TextView.VisualLines.ToList();
		}

		[Fact]
		public void TypingInAnUnfoldedProcedure_KeepsTheOtherVisualLines()
		{
			UiThread.Run(() =>
			{
				using Host host = new Host();
				EditorSession session = host.Session;
				DocumentViewModel document = host.Document;
				List<VisualLine> before = Lines(session);
				Assert.True(before.Count > 30, "visual lines: " + before.Count + ", view height " + session.Context.TextView.ActualHeight + ", editor " + session.Context.Editor.ActualHeight);
				Int32 offset = document.TextDocument.GetLineByNumber(30).EndOffset;

				document.TextDocument.Insert(offset, "x");
				List<VisualLine> after = Lines(session);

				Assert.Equal(before.Count, after.Count);
				Int32 kept = after.Count(line => before.Contains(line));
				Assert.True(kept >= after.Count - 2, "only " + kept + " of " + after.Count + " visual lines were kept");
			});
		}

		[Fact]
		public void EditingInsideAFoldedProcedure_StillRedrawsIt()
		{
			UiThread.Run(() =>
			{
				using Host host = new Host();
				EditorSession session = host.Session;
				DocumentViewModel document = host.Document;
				FoldingSection folded = session.Folding.ProcedureSections.First();
				folded.IsFolded = true;
				List<VisualLine> before = Lines(session);
				Int32 firstLine = document.TextDocument.GetLineByOffset(folded.StartOffset).LineNumber;
				VisualLine foldedLine = before.Single(line => line.FirstDocumentLine.LineNumber == firstLine);
				Assert.True(foldedLine.LastDocumentLine.LineNumber > firstLine);

				document.TextDocument.Insert(document.TextDocument.GetLineByNumber(firstLine + 3).EndOffset, "x");
				List<VisualLine> after = Lines(session);

				Assert.True(folded.IsFolded);
				VisualLine foldedAfter = after.Single(line => line.FirstDocumentLine.LineNumber == firstLine);
				Assert.NotSame(foldedLine, foldedAfter);
				Assert.True(foldedAfter.LastDocumentLine.LineNumber > firstLine);
			});
		}

		[Fact]
		public void AfterTheEdits_ProceduresCanStillBeFoldedAndUnfolded()
		{
			UiThread.Run(() =>
			{
				using Host host = new Host();
				EditorSession session = host.Session;
				DocumentViewModel document = host.Document;
				for (Int32 i = 0; i < 5; i++)
				{
					document.TextDocument.Insert(document.TextDocument.GetLineByNumber(20 + i).EndOffset, "x");
				}

				document.TextDocument.BeginUpdate();
				document.TextDocument.Insert(0, "// a\r\n");
				document.TextDocument.Insert(0, "// b\r\n");
				document.TextDocument.EndUpdate();

				FoldingSection section = session.Folding.ProcedureSections.First();
				Int32 inside = document.TextDocument.GetLineByOffset(section.StartOffset).LineNumber + 4;
				Assert.Contains(Lines(session), line => line.FirstDocumentLine.LineNumber == inside);

				section.IsFolded = true;
				Assert.DoesNotContain(Lines(session), line => line.FirstDocumentLine.LineNumber == inside);

				section.IsFolded = false;
				Assert.Contains(Lines(session), line => line.FirstDocumentLine.LineNumber == inside);
			});
		}
	}
}
