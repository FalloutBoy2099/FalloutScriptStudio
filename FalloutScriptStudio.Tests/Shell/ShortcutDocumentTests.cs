namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Xml.Linq;
	using FalloutScriptStudio.Tests.Accessibility;
	using Xunit;

	/// <summary>
	/// <c>docs\SHORTCUTS.md</c> lists every bound shortcut. The tables are generated from the bindings (see <see cref="ShortcutCatalog"/>) and these
	/// tests fail when the document and the bindings differ, or when the same gesture is bound twice in one place.
	/// </summary>
	public sealed class ShortcutDocumentTests
	{
		private const String ManualHeading = "## Keys handled by the controls";

		private static readonly String _manual = String.Join(
			"\n",
			ManualHeading,
			"",
			"These keys are not bindings of a window: the controls themselves handle them. The tests do not check this part.",
			"",
			"### Dialogs",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Enter | Activates the default button of the dialog (OK, Save, Go...) |",
			"| Esc | Cancels the dialog; in a tool window without a Cancel button it closes the window |",
			"| Alt + the underlined letter | Activates the button, check box or option that has that access key |",
			"| Tab, Shift+Tab | Moves the focus to the next or to the previous control |",
			"| Ctrl+Enter | Accepts the user function, rename and similar dialogs from inside a multi-line box |",
			"",
			"### Menus",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Alt, F10 | Activates the menu bar; the underlined letter of a menu opens it |",
			"| Left, Right, Up, Down | Moves between the menus and their items; Enter chooses, Esc closes |",
			"| Menu key, Shift+F10 | Opens the context menu of the control that has the focus |",
			"",
			"### Procedures, Variables, Functions and Solution panels",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Up, Down, Left, Right, Home, End | Moves in the tree, expands and collapses |",
			"| Enter | Procedures and Variables: goes to the declaration. Functions: inserts the function. Solution: opens the file. A folder or a group is expanded or collapsed |",
			"| Ctrl+Enter | Procedures and Variables: inserts the name at the caret |",
			"| F2 | Procedures and Variables: renames the symbol. Functions: renames the category |",
			"| Delete | Procedures: deletes the procedure. Functions: deletes the function or the empty category |",
			"| Alt+Up, Alt+Down | Procedures: moves the procedure up or down |",
			"| Ctrl+C | Solution: copies the full path of the file |",
			"",
			"### Errors, Problems and search results (also Find in Files)",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Up, Down | Moves to the previous or to the next row |",
			"| Enter | Goes to the place of the row in the editor |",
			"| Ctrl+C | Copies the row |",
			"| Esc | Stops the running build |",
			"",
			"### Search bar and Go to procedure",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Enter, Shift+Enter | Finds the next or the previous match; in the replace box, replaces |",
			"| Esc | Closes the search bar or the list |",
			"| Down | In the filter of the Go to procedure list, moves into the list |",
			"",
			"### Message grid",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Enter | Edits the current cell |",
			"| Delete, Backspace | Starts editing with an empty cell (Esc brings the old text back) |",
			"| Ctrl+Enter, Shift+Enter | Inserts an empty line below or above; inside a message text it inserts a line break |",
			"| Ctrl+Delete | Deletes the selected rows |",
			"| Ctrl+Up, Ctrl+Down | Moves the selected rows |",
			"| Ctrl+F | Goes to the search box |",
			"| Esc | Leaves the search box, the details pane or the window |",
			"",
			"### Dialogue preview",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Enter | Follows the selected option |",
			"| Ctrl+Enter | Shows the code of the selected row in the editor |",
			"| F2 | Edits the message of the selected row |",
			"",
			"### Flowchart canvas",
			"",
			"| Key | Action |",
			"| --- | --- |",
			"| Ctrl+A | Selects all the nodes |",
			"| Ctrl+0, Ctrl+Plus, Ctrl+Minus | Resets, increases or decreases the zoom |",
			"| Delete | Deletes the selected nodes |",
			"| Esc | Clears the selection |",
			"");

		private static String ShortcutsPath => Path.Combine(SourceTree.Root, "docs", "SHORTCUTS.md");

		[Fact]
		public void TheDocumentListsEveryBoundShortcut_AndOnlyThose()
		{
			Assert.True(File.Exists(ShortcutsPath), @"docs\SHORTCUTS.md is missing: run the tests with FSS_WRITE_SHORTCUTS=1 to generate it.");
			HashSet<String> documented = ShortcutCatalog.Documented(File.ReadAllText(ShortcutsPath));
			HashSet<String> bound = ShortcutCatalog.Bound();

			Assert.True(bound.Count > 60, "Too few shortcuts were found: the catalog is broken.");
			Findings.AssertNone(bound.Except(documented).OrderBy(pair => pair, StringComparer.Ordinal).Select(pair => "not documented: " + pair)
				.Concat(documented.Except(bound).OrderBy(pair => pair, StringComparer.Ordinal).Select(pair => "documented but not bound: " + pair)));
		}

		[Fact]
		public void TheGeneratedTablesAreExactlyWhatTheGeneratorWrites()
		{
			String document = File.ReadAllText(ShortcutsPath);
			String expected = ShortcutCatalog.Render(document[document.IndexOf(ManualHeading, StringComparison.Ordinal)..]);

			Assert.Equal(expected, document);
		}

		[Fact]
		public void NoGestureIsBoundTwiceInTheMainWindowOrToTheEditor()
		{
			IReadOnlyList<(String Scope, String Gesture, String Description)> entries = ShortcutCatalog.Read();
			String[] editor = entries.Where(entry => entry.Scope == "Code editor").Select(entry => entry.Gesture).ToArray();
			String[] window = entries.Where(entry => entry.Scope == "Main window").Select(entry => entry.Gesture).ToArray();

			Findings.AssertNone(window.GroupBy(gesture => gesture).Where(group => group.Count() > 1).Select(group => "bound twice in the main window: " + group.Key)
				.Concat(editor.GroupBy(gesture => gesture).Where(group => group.Count() > 1).Select(group => "bound twice in the code editor: " + group.Key))
				.Concat(window.Intersect(editor).Select(gesture => "bound by the main window and by the code editor: " + gesture)));
		}

		[Fact]
		public void TheSameGestureMeansTheSameThingInEveryWindow()
		{
			// The same gesture may be reused by different windows (they never have the focus together), but the editor and the main window
			// share the focus path, so the dialogs must not reuse a gesture of the main window for something else.
			IReadOnlyList<(String Scope, String Gesture, String Description)> entries = ShortcutCatalog.Read();
			HashSet<String> main = entries.Where(entry => entry.Scope == "Main window").Select(entry => entry.Gesture).ToHashSet(StringComparer.Ordinal);
			Findings.AssertNone(entries
				.Where(entry => entry.Scope is not ("Main window" or "Code editor") && main.Contains(entry.Gesture) && !AllowedReuse.Contains(entry.Scope + "|" + entry.Gesture))
				.Select(entry => $"{entry.Scope}: {entry.Gesture} ({entry.Description}) is also a shortcut of the main window"));
		}

		/// <summary>The dialogs that deliberately reuse a gesture of the main window, because they offer the same action in their own context.</summary>
		private static readonly HashSet<String> AllowedReuse = new HashSet<String>(StringComparer.Ordinal)
		{
			// The message editor is a document editor of its own, with the file shortcuts of the main window.
			"Message editor window|Ctrl+N", "Message editor window|Ctrl+O", "Message editor window|Ctrl+S", "Message editor window|Ctrl+Shift+S",
			"Message editor window|F3", "Message editor window|Shift+F3", "Message editor window|F8", "Message editor window|Shift+F8",
			"Message editor window|F5", "Message editor window|Ctrl+Z", "Message editor window|Ctrl+Y", "Message editor window|Ctrl++",
			"Message editor window|Ctrl+-", "Message editor window|Ctrl+D", "Message editor window|Alt+A", "Message editor window|Alt+C",
			"Message editor window|Alt+S", "Message editor window|Ctrl+Shift+Z",
			"Flowchart window|F5", "Flowchart window|Alt+E",
			"Node code editor window|Ctrl+Z", "Node code editor window|Ctrl+Y",
		};

		[Fact]
		public void EveryShortcutShownInAnyMenuIsBoundOrHandledInCode()
		{
			// Gestures that the menus of the other windows show and that the controls handle in code: the code file must handle the key.
			(String View, String Gesture, String CodeFile, String? Token)[] handledInCode =
			{
				("MessageEditorWindow.xaml", "Ctrl+Enter", "MessageGridController.cs", "Key.Enter"),
				("MessageEditorWindow.xaml", "Shift+Enter", "MessageGridController.cs", "Key.Enter"),
				("MessageEditorWindow.xaml", "Ctrl+Up", "MessageGridController.cs", "Key.Up"),
				("MessageEditorWindow.xaml", "Ctrl+Down", "MessageGridController.cs", "Key.Down"),
				("MessageEditorWindow.xaml", "Ctrl+Delete", "MessageGridController.cs", "Key.Delete"),
				("FlowchartWindow.xaml", "Del", "DiagramCanvas.Input.cs", "Key.Delete"),
				("NodeCodeEditorWindow.xaml", "Del", "NodeCodeEditorWindow.xaml", "ApplicationCommands.Delete"),
			};
			String[] clipboard = { "Ctrl+X", "Ctrl+C", "Ctrl+V", "Ctrl+Z", "Ctrl+Y", "Ctrl+A", "Alt+F4" };
			List<String> problems = new List<String>();

			foreach (String file in SourceTree.ViewFiles())
			{
				XDocument document = SourceTree.Load(file);
				HashSet<String> bound = document.Descendants(SourceTree.Presentation + "KeyBinding").Select(ShortcutCatalog.Normalize).ToHashSet(StringComparer.Ordinal);
				bound.UnionWith(ShortcutCatalog.Read().Where(entry => entry.Scope is "Main window" or "Code editor").Select(entry => entry.Gesture));

				foreach (XElement item in document.Descendants(SourceTree.Presentation + "MenuItem").Where(e => e.Attribute("InputGestureText") is not null))
				{
					foreach (String gesture in ((String)item.Attribute("InputGestureText")!).Split(new[] { " / ", "," }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
					{
						if (bound.Contains(gesture) || clipboard.Contains(gesture) || gesture == "MMB")
						{
							continue;
						}

						(String View, String Gesture, String CodeFile, String? Token) known = handledInCode.FirstOrDefault(entry => file.EndsWith(entry.View, StringComparison.Ordinal) && entry.Gesture == gesture);
						Boolean handled = known.CodeFile is not null && (known.Token is null || Directory.EnumerateFiles(SourceTree.AppFolder, known.CodeFile, SearchOption.AllDirectories).Any(code => File.ReadAllText(code).Contains(known.Token, StringComparison.Ordinal)));
						if (!handled)
						{
							problems.Add($"{SourceTree.Relative(file)}: the menu shows {gesture} but nothing handles it");
						}
					}
				}
			}

			Findings.AssertNone(problems);
		}

		[Fact]
		public void WriteTheDocument()
		{
			if (Environment.GetEnvironmentVariable("FSS_WRITE_SHORTCUTS") != "1")
			{
				return;
			}

			String manual = File.Exists(ShortcutsPath) && File.ReadAllText(ShortcutsPath) is { } existing && existing.Contains(ManualHeading, StringComparison.Ordinal)
				? existing[existing.IndexOf(ManualHeading, StringComparison.Ordinal)..]
				: _manual.Replace("\n", "\r\n", StringComparison.Ordinal);
			File.WriteAllText(ShortcutsPath, ShortcutCatalog.Render(manual), new UTF8Encoding(false));
		}
	}
}
