namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;

	/// <content>Rules: dialog conventions, layout and reading order of a window.</content>
	internal sealed partial class XamlAccessibilityScanner
	{
		/// <summary>
		/// Windows where Escape belongs to the editor inside (it dismisses the completion list and the tips), so the window is closed with
		/// its Done button and not with Escape.
		/// </summary>
		private static readonly HashSet<String> _escapeBelongsToEditor = new(StringComparer.Ordinal) { "NodeCodeEditorWindow.xaml" };

		/// <summary>Finds the problems of the conventions of a window: Enter, Escape, initial focus, title.</summary>
		public IReadOnlyList<String> FindWindowConventionViolations(String file)
		{
			return this.FindWindowConventionViolations(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindWindowConventionViolations(String file, XDocument document)
		{
			XElement root = document.Root!;
			List<String> findings = [];
			Boolean hasDefault = document.Descendants().Any(e => IsTrue(e, "IsDefault"));
			Boolean hasCancel = document.Descendants().Any(e => IsTrue(e, "IsCancel"));
			Boolean closesOnEscape = document.Descendants().Any(e => e.Attributes().Any(a => a.Name.LocalName.EndsWith("CloseOnEscape", StringComparison.Ordinal) && IsTrueText(a.Value)))
				|| root.Attributes().Any(a => a.Name.LocalName.EndsWith("CloseOnEscape", StringComparison.Ordinal));
			Boolean hasFocus = root.Attributes().Any(a => a.Name.LocalName == "FocusedElement" || a.Name.LocalName.EndsWith("FocusedElement", StringComparison.Ordinal))
				|| document.Descendants().Any(e => e.Attributes().Any(a => a.Name.LocalName.EndsWith("FocusedElement", StringComparison.Ordinal)));
			String codeBehind = file + ".cs";
			String code = File.Exists(codeBehind) ? File.ReadAllText(codeBehind) : String.Empty;
			Boolean focusesInCode = code.Contains(".Focus()", StringComparison.Ordinal);
			Boolean escapeInCode = code.Contains("Key.Escape", StringComparison.Ordinal);

			if (root.Attribute("Title") is null)
			{
				findings.Add(Describe(file, root, "window without a Title"));
			}

			if (!hasCancel && !closesOnEscape && !escapeInCode && !_escapeBelongsToEditor.Contains(Path.GetFileName(file)))
			{
				findings.Add(Describe(file, root, "Escape does not close the window: add a button with IsCancel or WindowKeys.CloseOnEscape"));
			}

			if (hasDefault && !hasCancel && !closesOnEscape && !escapeInCode)
			{
				findings.Add(Describe(file, root, "default button without a cancel button"));
			}

			if (!hasFocus && !focusesInCode)
			{
				findings.Add(Describe(file, root, "no initial focus: set FocusManager.FocusedElement on the window or focus the main field in code"));
			}

			return findings;
		}

		/// <summary>Finds windows that would be hard to use at a high display scale or that render blurry lines.</summary>
		public IReadOnlyList<String> FindLayoutProblems(String file, XDocument document)
		{
			XElement root = document.Root!;
			List<String> findings = [];
			if (!String.Equals((String?)root.Attribute("UseLayoutRounding"), "True", StringComparison.Ordinal) || !String.Equals((String?)root.Attribute("SnapsToDevicePixels"), "True", StringComparison.Ordinal))
			{
				findings.Add(Describe(file, root, "window without UseLayoutRounding and SnapsToDevicePixels set to True"));
			}

			if (Double.TryParse((String?)root.Attribute("Height"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Double height)
				&& height >= 480
				&& !root.Attributes().Any(attribute => attribute.Name.LocalName == "WindowFit.FitToWorkArea"))
			{
				findings.Add(Describe(file, root, "window of " + height + " units without WindowFit.FitToWorkArea: it would not fit on a small screen at 200% scale"));
			}

			foreach (XElement element in document.Descendants().Where(e => e.Attribute("UseLayoutRounding") is not null && e != root))
			{
				if (String.Equals((String?)element.Attribute("UseLayoutRounding"), "False", StringComparison.Ordinal))
				{
					findings.Add(Describe(file, element, "UseLayoutRounding turned off below the window"));
				}
			}

			return findings;
		}

		/// <summary>Finds the windows whose Tab order does not follow the reading order of their grid cells.</summary>
		public IReadOnlyList<String> FindReadingOrderProblems(String file, XDocument document)
		{
			List<String> findings = [];
			foreach (XElement grid in document.Descendants().Where(e => e.Name.LocalName == "Grid"))
			{
				(Int32 Row, Int32 Column)? previous = null;
				foreach (XElement child in grid.Elements().Where(c => !c.Name.LocalName.Contains('.', StringComparison.Ordinal)))
				{
					// Popups and overlays that cover several cells do not take part in the order; an explicit TabIndex is the author's decision.
					if (child.Name.LocalName == "Popup"
						|| child.Attribute("Grid.RowSpan") is not null
						|| child.Attribute("Grid.ColumnSpan") is not null
						|| child.DescendantsAndSelf().Any(e => e.Attribute("TabIndex") is not null)
						|| !child.DescendantsAndSelf().Any(e => _buttonLike.Contains(e.Name.LocalName) || _inputLike.Contains(e.Name.LocalName) && e.Name.LocalName != "ProgressBar"))
					{
						continue;
					}

					(Int32 Row, Int32 Column) position = (CellOf(child, "Grid.Row"), CellOf(child, "Grid.Column"));
					if (previous is { } last && (position.Row < last.Row || (position.Row == last.Row && position.Column < last.Column)))
					{
						findings.Add(Describe(file, child, "comes after a control that sits further down or to the right in the same grid, so the Tab order differs from the reading order"));
					}

					previous = position;
				}
			}

			return findings;
		}

		private static Int32 CellOf(XElement element, String attribute)
		{
			return Int32.TryParse((String?)element.Attribute(attribute), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Int32 value) ? value : 0;
		}
	}
}