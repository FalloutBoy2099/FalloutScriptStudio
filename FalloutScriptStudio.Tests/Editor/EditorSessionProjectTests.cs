namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;

	/// <summary>The editor commands that use the project: navigation to symbols of other files, the refactorings and the unused variables.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSessionProjectTests
	{
		private static readonly String Script = String.Join(
			"\r\n",
			"#include \"common.h\"",
			"procedure declared_only;",
			"procedure local_proc;",
			"",
			"procedure start begin",
			"   variable unused, used;",
			"   used = remote_proc(3);",
			"   declared_only();",
			"   local_proc();",
			"   display_msg(REMOTE_MACRO + used);",
			"end",
			"",
			"procedure local_proc begin",
			"   display_msg(\"x\");",
			"end");

		private static Int32 OffsetOf(DocumentViewModel document, String text, Int32 inside = 1)
		{
			return document.TextDocument.Text.IndexOf(text, StringComparison.Ordinal) + inside;
		}

		[Fact]
		public void GoToDefinition_OfAnUnknownName_IsOfferedWhenTheProjectKnowsIt_AndAsksTheWorkspace()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOf(script, "remote_proc");

				Assert.False(surface.CanExecute(EditorCommand.GoToDefinition));
				env.Recorder.ProjectSymbols.Add("remote_proc");
				Assert.True(surface.CanExecute(EditorCommand.GoToDefinition));
				Assert.True(surface.CanExecute(EditorCommand.GoToDeclaration));
				Assert.True(surface.CanExecute(EditorCommand.FindReferences));
				surface.Execute(EditorCommand.GoToDefinition);
				surface.Execute(EditorCommand.GoToDeclaration);
				surface.Execute(EditorCommand.FindReferences);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("NavigateToProjectSymbolAsync(", StringComparison.Ordinal) && call.EndsWith("remote_proc, Definition)", StringComparison.Ordinal));
				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("NavigateToProjectSymbolAsync(", StringComparison.Ordinal) && call.EndsWith("remote_proc, Declaration)", StringComparison.Ordinal));
				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("NavigateToProjectSymbolAsync(", StringComparison.Ordinal) && call.EndsWith("remote_proc, References)", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void GoToDefinition_OfAMacroUnknownToTheDocument_UsesTheProject()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				env.Recorder.ProjectSymbols.Add("REMOTE_MACRO");
				surface.CaretOffset = OffsetOf(script, "REMOTE_MACRO");

				Assert.True(surface.CanExecute(EditorCommand.GoToDefinition));
				surface.Execute(EditorCommand.GoToDefinition);

				Assert.Contains(env.Recorder.Calls, call => call.EndsWith("REMOTE_MACRO, Definition)", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void GoToDefinition_OfAProcedureThatIsOnlyDeclared_LooksForItsBodyInTheProject()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOf(script, "declared_only();");

				env.Recorder.ProjectSymbols.Add("declared_only");
				Assert.True(surface.CanExecute(EditorCommand.GoToDefinition));
				surface.Execute(EditorCommand.GoToDefinition);

				Assert.Contains(env.Recorder.Calls, call => call.EndsWith("declared_only, Definition)", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void GoToDefinition_OfAProcedureWithABodyInTheDocument_StaysInTheDocument()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				env.Recorder.ProjectSymbols.Add("local_proc");
				surface.CaretOffset = OffsetOf(script, "local_proc();");

				surface.Execute(EditorCommand.GoToDefinition);

				Assert.DoesNotContain(env.Recorder.Calls, call => call.StartsWith("NavigateToProjectSymbolAsync(", StringComparison.Ordinal));
				Assert.Equal(13, surface.CaretLine);
			});
		}

		[Fact]
		public void FindReferences_OfAProcedureOfTheDocument_AlsoSearchesTheProject()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				env.Recorder.ProjectSymbols.Add("local_proc");
				surface.CaretOffset = OffsetOf(script, "local_proc();");

				surface.Execute(EditorCommand.FindReferences);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("ShowProjectReferencesAsync(", StringComparison.Ordinal) && call.EndsWith("local_proc)", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void GoToDefinition_WithoutAProjectSymbol_IsNotOffered()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOf(script, "display_msg(REMOTE");

				Assert.False(surface.CanExecute(EditorCommand.GoToDefinition));
				Assert.False(surface.CanExecute(EditorCommand.GoToDeclaration));
				Assert.False(surface.CanExecute(EditorCommand.FindReferences));
			});
		}

		[Fact]
		public void ExtractProcedure_NeedsASelection_AndAsksTheWorkspace()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOf(script, "local_proc();");
				Assert.False(surface.CanExecute(EditorCommand.ExtractProcedure));

				Int32 start = OffsetOf(script, "declared_only();", 0);
				surface.Select(start, "declared_only();".Length);
				Assert.True(surface.CanExecute(EditorCommand.ExtractProcedure));
				surface.Execute(EditorCommand.ExtractProcedure);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("ExtractProcedureAsync(", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void DeleteUnusedProcedure_WorksOnTheProcedureUnderTheCaret()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = 3;
				Assert.False(surface.CanExecute(EditorCommand.DeleteUnusedProcedure));

				surface.CaretOffset = OffsetOf(script, "display_msg(\"x\")");
				Assert.True(surface.CanExecute(EditorCommand.DeleteUnusedProcedure));
				surface.Execute(EditorCommand.DeleteUnusedProcedure);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("DeleteUnusedProcedureAsync(", StringComparison.Ordinal) && call.EndsWith("local_proc)", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void FindUnusedVariables_AsksTheWorkspace()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);

				Assert.True(surface.CanExecute(EditorCommand.FindUnusedVariables));
				surface.Execute(EditorCommand.FindUnusedVariables);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("FindUnusedVariablesAsync(", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void RemoveUnusedVariable_RemovesTheDeclarationWithOneUndo()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(String.Join("\r\n", "procedure start begin", "   variable unused, used;", "   used = 1;", "   display_msg(used);", "end"));
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				String original = script.TextDocument.Text;

				surface.CaretOffset = OffsetOf(script, "used;", 0);
				Assert.False(surface.CanExecute(EditorCommand.RemoveUnusedVariable));

				surface.CaretOffset = OffsetOf(script, "unused", 2);
				Assert.True(surface.CanExecute(EditorCommand.RemoveUnusedVariable));
				surface.Execute(EditorCommand.RemoveUnusedVariable);

				Assert.Contains("variable used;", script.TextDocument.Text, StringComparison.Ordinal);
				Assert.DoesNotContain("unused", script.TextDocument.Text, StringComparison.Ordinal);
				surface.Undo();
				Assert.Equal(original, script.TextDocument.Text);
			});
		}

		[Fact]
		public void RemoveUnusedVariable_IsNotOfferedForAVariableWhoseInitializerCallsSomething()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(String.Join("\r\n", "procedure start begin", "   variable unused = random(1, 5);", "end"));
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOf(script, "unused", 2);

				Assert.False(surface.CanExecute(EditorCommand.RemoveUnusedVariable));
			});
		}

		[Fact]
		public void TheOptionMarksTheUnusedLocalVariablesWithAFaintHint()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(String.Join("\r\n", "variable global_unused;", "procedure start begin", "   variable unused, used;", "   used = 1;", "   display_msg(used);", "end"));
				using EditorSession session = env.StartSession(script);
				Int32 offset = OffsetOf(script, "unused,", 1);

				session.Diagnostics.Update();
				Assert.Empty(session.Diagnostics.GetMarkersAt(offset));

				env.SettingsService.Update(settings => settings.Editor.ShowUnusedVariables = true);
				session.Diagnostics.Update();

				DiagnosticMarker marker = Assert.Single(session.Diagnostics.GetMarkersAt(offset));
				Assert.Equal(MarkerSeverity.Unused, marker.Severity);
				Assert.Contains("unused", marker.Message, StringComparison.Ordinal);
				Assert.Empty(session.Diagnostics.GetMarkersAt(OffsetOf(script, "global_unused", 1)));
				Assert.Empty(session.Diagnostics.GetMarkersAt(script.TextDocument.Text.IndexOf(", used;", StringComparison.Ordinal) + 3));
			});
		}

		[Fact]
		public void TheUnusedMarkFollowsTheTextAfterANewParse()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Update(settings => settings.Editor.ShowUnusedVariables = true);
				DocumentViewModel script = env.CreateDocument(String.Join("\r\n", "procedure start begin", "   variable a;", "end"));
				using EditorSession session = env.StartSession(script);
				Int32 offset = OffsetOf(script, "a;", 0);
				session.Diagnostics.Update();
				Assert.Single(session.Diagnostics.GetMarkersAt(offset));

				script.TextDocument.Insert(script.TextDocument.Text.IndexOf("end", StringComparison.Ordinal), "   display_msg(a);\r\n");
				session.Diagnostics.Update();

				Assert.Empty(session.Diagnostics.GetMarkersAt(offset));
			});
		}

		[Fact]
		public void TheEditorCommandsOfTheProjectHaveGesturesOrMenuItems()
		{
			Assert.Equal(
				new[] { "Ctrl+Shift+E" },
				EditorCommands.All
					.Where(pair => pair.Key is EditorCommand.ExtractProcedure or EditorCommand.DeleteUnusedProcedure or EditorCommand.FindUnusedVariables or EditorCommand.RemoveUnusedVariable)
					.SelectMany(pair => pair.Value.InputGestures.OfType<System.Windows.Input.KeyGesture>())
					.Select(gesture => gesture.GetDisplayStringForCulture(System.Globalization.CultureInfo.InvariantCulture))
					.ToArray());
		}
	}
}
