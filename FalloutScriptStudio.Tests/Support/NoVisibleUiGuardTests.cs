namespace FalloutScriptStudio.Tests.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// The automated tests must be invisible: they run while the developer works, so no window, popup, tooltip, menu or message box may flash on
	/// the screen and nothing may take the focus. This guard reads the sources of the test project and fails when a test shows a real
	/// top-level window by itself instead of going through <see cref="OffscreenWindow"/> (or the fakes such as <see cref="HiddenEditorPopups"/>).
	/// </summary>
	public sealed class NoVisibleUiGuardTests
	{
		/// <summary>The constructs that open or create a real top-level window.</summary>
		private static readonly (String Name, Regex Pattern)[] Forbidden =
		{
			("Show() without argument (Window.Show)", new Regex(@"\.Show\s*\(\s*\)", RegexOptions.Compiled)),
			("ShowDialog()", new Regex(@"\.ShowDialog\s*\(", RegexOptions.Compiled)),
			("Activate()", new Regex(@"\.Activate\s*\(\s*\)", RegexOptions.Compiled)),
			("new <...>Window", new Regex(@"\bnew\s+(?:[A-Za-z_][\w]*\.)*\w*Window\b", RegexOptions.Compiled)),
			("new Popup", new Regex(@"\bnew\s+(?:[A-Za-z_][\w]*\.)*Popup\b", RegexOptions.Compiled)),
			("IsOpen = true", new Regex(@"\bIsOpen\s*=\s*true\b", RegexOptions.Compiled)),
			("MessageBox.Show", new Regex(@"\bMessageBox\s*\.\s*Show\b", RegexOptions.Compiled)),
			("Topmost = true", new Regex(@"\bTopmost\s*=\s*true\b", RegexOptions.Compiled)),
		};

		/// <summary>Files that are allowed to use the constructs (path relative to the test project, with a justification).</summary>
		private static readonly Dictionary<String, String> AllowedFiles = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
		{
			// The one place that creates and shows real windows: off-screen, not activated, without taskbar button.
			["Support/OffscreenWindow.cs"] = "the shared off-screen window helper",
			// The guard describes the forbidden constructs.
			["Support/NoVisibleUiGuardTests.cs"] = "this guard",
		};

		/// <summary>Folders that are allowed to use the constructs (owned by the accessibility tests, which show every window through their own off-screen host).</summary>
		private static readonly String[] AllowedFolders = { "Accessibility/" };

		[Fact]
		public void TheTests_NeverShowARealWindowOrPopupByThemselves()
		{
			String root = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio.Tests");
			List<String> offenders = new List<String>();

			foreach (String file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				String relative = Path.GetRelativePath(root, file).Replace('\\', '/');
				if (relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
					|| relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
					|| AllowedFiles.ContainsKey(relative)
					|| AllowedFolders.Any(folder => relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
				{
					continue;
				}

				String[] lines = StripCommentsAndStrings(File.ReadAllText(file)).Split('\n');
				for (Int32 index = 0; index < lines.Length; index++)
				{
					foreach ((String name, Regex pattern) in Forbidden)
					{
						if (pattern.IsMatch(lines[index]))
						{
							offenders.Add($"{relative}({index + 1}): {name}");
						}
					}
				}
			}

			Assert.True(
				offenders.Count == 0,
				"The tests must not show real windows (they flash on the screen and steal the focus). Use Support/OffscreenWindow, HiddenEditorPopups or a fake presenter:"
				+ Environment.NewLine
				+ String.Join(Environment.NewLine, offenders));
		}

		[Fact]
		public void TheGuard_RecognizesTheForbiddenConstructs()
		{
			String[] samples =
			{
				"window.Show();",
				"dialog.ShowDialog();",
				"window.Activate();",
				"Window w = new Window { Content = x };",
				"var w = new System.Windows.Window();",
				"var w = new FluentWindow();",
				"popup = new Popup();",
				"menu.IsOpen = true;",
				"MessageBox.Show(\"x\");",
			};

			foreach (String sample in samples)
			{
				Assert.Contains(Forbidden, rule => rule.Pattern.IsMatch(StripCommentsAndStrings(sample)));
			}

			String[] allowed =
			{
				"harness.Search.Show(false);",
				"new WindowPlacement { X = 3 };",
				"// window.Show();",
				"String text = \"window.Show()\";",
				"session.Completion.Show(true);",
			};

			foreach (String sample in allowed)
			{
				String stripped = StripCommentsAndStrings(sample);
				Assert.DoesNotContain(Forbidden, rule => rule.Pattern.IsMatch(stripped));
			}
		}

		private static String StripCommentsAndStrings(String source)
		{
			String withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", match => new String('\n', match.Value.Count(c => c == '\n')), RegexOptions.Singleline);
			String withoutStrings = Regex.Replace(withoutBlocks, "@\"(?:[^\"]|\"\")*\"|\"(?:\\\\.|[^\"\\\\\\n])*\"", "\"\"");
			return Regex.Replace(withoutStrings, @"//[^\n]*", String.Empty);
		}
	}
}
