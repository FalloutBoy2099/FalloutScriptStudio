namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Windows;
	using System.Windows.Automation;
	using Condition = System.Windows.Automation.Condition;

	/// <summary>Helpers over UI Automation: finding elements with timeouts, using the patterns (never the mouse or the keyboard focus), and dumping a tree.</summary>
	internal static class Uia
	{
		/// <summary>The default time a wait for an element or a state lasts.</summary>
		public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

		/// <summary>Takes the properties of a whole tree in one round trip.</summary>
		/// <param name="root">The root of the tree.</param>
		/// <returns>The elements below the root with their properties.</returns>
		public static IReadOnlyList<UiNode> Snapshot(AutomationElement root)
		{
			CacheRequest request = new();
			request.Add(AutomationElement.NameProperty);
			request.Add(AutomationElement.ControlTypeProperty);
			request.Add(AutomationElement.AutomationIdProperty);
			request.Add(AutomationElement.BoundingRectangleProperty);
			request.Add(AutomationElement.IsOffscreenProperty);
			request.Add(AutomationElement.IsEnabledProperty);
			request.Add(AutomationElement.ClassNameProperty);
			request.Add(AutomationElement.HelpTextProperty);
			request.TreeScope = TreeScope.Element;
			using (request.Activate())
			{
				AutomationElementCollection all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
				List<UiNode> nodes = new(all.Count);
				foreach (AutomationElement element in all)
				{
					try
					{
						nodes.Add(new UiNode(element));
					}
					catch (Exception ex) when (ex is ElementNotAvailableException or COMException)
					{
						// The element vanished while the tree was read.
					}
				}

				return nodes;
			}
		}

		/// <summary>Finds the first element below a root.</summary>
		/// <param name="root">The root.</param>
		/// <param name="name">The exact name, or <see langword="null"/> for any.</param>
		/// <param name="type">The control type, or <see langword="null"/> for any.</param>
		/// <param name="id">The automation id, or <see langword="null"/> for any.</param>
		/// <returns>The element, or <see langword="null"/>.</returns>
		public static AutomationElement? Find(AutomationElement root, String? name = null, ControlType? type = null, String? id = null)
		{
			List<Condition> conditions = [];
			if (name is not null)
			{
				conditions.Add(new PropertyCondition(AutomationElement.NameProperty, name));
			}

			if (type is not null)
			{
				conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, type));
			}

			if (id is not null)
			{
				conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, id));
			}

			Condition condition = conditions.Count switch
			{
				0 => Condition.TrueCondition,
				1 => conditions[0],
				_ => new AndCondition(conditions.ToArray()),
			};
			try
			{
				return root.FindFirst(TreeScope.Descendants, condition);
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or COMException)
			{
				return null;
			}
		}

		/// <summary>Finds the elements below a root whose normalized name contains a text.</summary>
		/// <param name="root">The root.</param>
		/// <param name="text">The text.</param>
		/// <param name="type">The control type, or <see langword="null"/> for any.</param>
		/// <returns>The elements.</returns>
		public static IReadOnlyList<AutomationElement> FindByNameContaining(AutomationElement root, String text, ControlType? type = null)
		{
			String wanted = Normalize(text);
			Condition condition = type is null ? Condition.TrueCondition : new PropertyCondition(AutomationElement.ControlTypeProperty, type);
			List<AutomationElement> found = [];
			try
			{
				foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition))
				{
					if (Normalize(element.Current.Name).Contains(wanted, StringComparison.Ordinal))
					{
						found.Add(element);
					}
				}
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or COMException)
			{
				// The tree changed; the caller polls again.
			}

			return found;
		}

		/// <summary>Waits until a probe returns a value.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="probe">Returns the value, or <see langword="null"/> while the state is not reached.</param>
		/// <param name="what">What is waited for (the failure message).</param>
		/// <param name="timeout">The time limit; <see cref="DefaultTimeout"/> when omitted.</param>
		/// <returns>The value.</returns>
		public static T Wait<T>(Func<T?> probe, String what, TimeSpan? timeout = null)
			where T : class
		{
			DateTime limit = DateTime.UtcNow + (timeout ?? DefaultTimeout);
			Exception? last = null;
			while (true)
			{
				try
				{
					T? value = probe();
					if (value is not null)
					{
						return value;
					}
				}
				catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or TimeoutException)
				{
					last = ex;
				}

				if (DateTime.UtcNow > limit)
				{
					throw new TimeoutException("Timed out waiting for " + what + (last is null ? "." : ": " + last.Message));
				}

				Thread.Sleep(80);
			}
		}

		/// <summary>Waits until a condition holds.</summary>
		/// <param name="condition">The condition.</param>
		/// <param name="what">What is waited for (the failure message).</param>
		/// <param name="timeout">The time limit; <see cref="DefaultTimeout"/> when omitted.</param>
		public static void WaitUntil(Func<Boolean> condition, String what, TimeSpan? timeout = null)
		{
			Wait(() => condition() ? new Object() : null, what, timeout);
		}

		/// <summary>Waits for an element below a root.</summary>
		/// <param name="root">The root.</param>
		/// <param name="name">The exact name, or <see langword="null"/> for any.</param>
		/// <param name="type">The control type, or <see langword="null"/> for any.</param>
		/// <param name="id">The automation id, or <see langword="null"/> for any.</param>
		/// <param name="timeout">The time limit.</param>
		/// <returns>The element.</returns>
		public static AutomationElement WaitFor(AutomationElement root, String? name = null, ControlType? type = null, String? id = null, TimeSpan? timeout = null)
		{
			String what = "the element " + Describe(name, type, id);
			return Wait(() => Find(root, name, type, id), what, timeout);
		}

		/// <summary>Waits until an element below a root is gone.</summary>
		/// <param name="root">The root.</param>
		/// <param name="name">The exact name.</param>
		/// <param name="type">The control type, or <see langword="null"/> for any.</param>
		/// <param name="timeout">The time limit.</param>
		public static void WaitGone(AutomationElement root, String name, ControlType? type = null, TimeSpan? timeout = null)
		{
			WaitUntil(() => Find(root, name, type) is null, "the element " + Describe(name, type, null) + " to go", timeout);
		}

		/// <summary>Invokes an element (a button, a menu item).</summary>
		/// <param name="element">The element.</param>
		public static void Invoke(AutomationElement element)
		{
			((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
		}

		/// <summary>Sets the text of a text box through its value pattern.</summary>
		/// <param name="element">The element.</param>
		/// <param name="text">The text.</param>
		public static void SetValue(AutomationElement element, String text)
		{
			((ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern)).SetValue(text);
		}

		/// <summary>Gets the value of an element.</summary>
		/// <param name="element">The element.</param>
		/// <returns>The value.</returns>
		public static String GetValue(AutomationElement element)
		{
			return ((ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
		}

		/// <summary>Selects an item of a list, a tab or a tree.</summary>
		/// <param name="element">The item.</param>
		public static void Select(AutomationElement element)
		{
			((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
		}

		/// <summary>Gets whether an item is selected.</summary>
		/// <param name="element">The item.</param>
		/// <returns><see langword="true"/> when it is selected.</returns>
		public static Boolean IsSelected(AutomationElement element)
		{
			return ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected;
		}

		/// <summary>Expands an element (a menu, a tree node, a combo box).</summary>
		/// <param name="element">The element.</param>
		public static void Expand(AutomationElement element)
		{
			((ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
		}

		/// <summary>Collapses an element.</summary>
		/// <param name="element">The element.</param>
		public static void Collapse(AutomationElement element)
		{
			((ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
		}

		/// <summary>Sets a check box or toggle button to a state.</summary>
		/// <param name="element">The element.</param>
		/// <param name="on">The wanted state.</param>
		public static void SetToggle(AutomationElement element, Boolean on)
		{
			TogglePattern pattern = (TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern);
			ToggleState wanted = on ? ToggleState.On : ToggleState.Off;
			for (Int32 attempt = 0; attempt < 3 && pattern.Current.ToggleState != wanted; attempt++)
			{
				pattern.Toggle();
			}
		}

		/// <summary>Closes a window through its window pattern.</summary>
		/// <param name="window">The window.</param>
		public static void CloseWindow(AutomationElement window)
		{
			((WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern)).Close();
		}

		/// <summary>Scrolls an element into view.</summary>
		/// <param name="element">The element.</param>
		public static void ScrollIntoView(AutomationElement element)
		{
			if (element.TryGetCurrentPattern(ScrollItemPattern.Pattern, out Object? pattern))
			{
				((ScrollItemPattern)pattern).ScrollIntoView();
			}
		}

		/// <summary>Scrolls the first vertically scrollable element below a root to a position (a page of the settings with its cards).</summary>
		/// <param name="root">The root.</param>
		/// <param name="percent">The position, from 0 (top) to 100 (end).</param>
		public static void ScrollTo(AutomationElement root, Double percent)
		{
			foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsScrollPatternAvailableProperty, true)))
			{
				ScrollPattern pattern = (ScrollPattern)element.GetCurrentPattern(ScrollPattern.Pattern);
				if (pattern.Current.VerticallyScrollable)
				{
					pattern.SetScrollPercent(ScrollPattern.NoScroll, percent);
					return;
				}
			}
		}

		/// <summary>Gets whether an element supports a pattern.</summary>
		/// <param name="element">The element.</param>
		/// <param name="pattern">The pattern.</param>
		/// <returns><see langword="true"/> when it does.</returns>
		public static Boolean Supports(AutomationElement element, AutomationPattern pattern)
		{
			return element.TryGetCurrentPattern(pattern, out _);
		}

		/// <summary>Removes the access key markers and the ellipsis from a text and compares it case-insensitively, as the command palette does.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The normalized text.</returns>
		public static String Normalize(String? text)
		{
			if (String.IsNullOrEmpty(text))
			{
				return String.Empty;
			}

			// Access key markers: "_File" (the automation name has none), and the Chinese "File(_F)" (shown as "File(F)").
			String clean = System.Text.RegularExpressions.Regex.Replace(text, @"\(_?\w\)\s*$", String.Empty);
			clean = clean.Replace("_", String.Empty, StringComparison.Ordinal).Replace("&", String.Empty, StringComparison.Ordinal).Trim();
			while (clean.EndsWith("...", StringComparison.Ordinal) || clean.EndsWith('…'))
			{
				clean = clean.TrimEnd('…').TrimEnd('.').TrimEnd();
			}

			return clean.ToLowerInvariant();
		}

		/// <summary>Writes the tree below an element as indented text (name, type, id, bounds): the picture of a window for the report.</summary>
		/// <param name="root">The root.</param>
		/// <param name="maxNodes">The most elements written.</param>
		/// <returns>The text.</returns>
		public static String Dump(AutomationElement root, Int32 maxNodes = 600)
		{
			StringBuilder text = new();
			Int32 count = 0;
			DumpNode(root, 0, text, ref count, maxNodes);
			return text.ToString();
		}

		private static void DumpNode(AutomationElement element, Int32 depth, StringBuilder text, ref Int32 count, Int32 maxNodes)
		{
			if (count >= maxNodes)
			{
				return;
			}

			count++;
			try
			{
				Rect rect = element.Current.BoundingRectangle;
				text.Append(' ', depth * 2)
					.Append(element.Current.ControlType.ProgrammaticName.Replace("ControlType.", String.Empty, StringComparison.Ordinal))
					.Append(" name='").Append(element.Current.Name).Append('\'');
				if (element.Current.AutomationId.Length > 0)
				{
					text.Append(" id=").Append(element.Current.AutomationId);
				}

				text.Append(" rect=").Append((Int32)rect.X).Append(',').Append((Int32)rect.Y).Append(' ').Append((Int32)rect.Width).Append('x').Append((Int32)rect.Height);
				if (element.Current.IsOffscreen)
				{
					text.Append(" offscreen");
				}

				text.AppendLine();
				AutomationElement? child = TreeWalker.RawViewWalker.GetFirstChild(element);
				while (child is not null)
				{
					DumpNode(child, depth + 1, text, ref count, maxNodes);
					child = TreeWalker.RawViewWalker.GetNextSibling(child);
				}
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or COMException)
			{
				text.Append(' ', depth * 2).AppendLine("(gone)");
			}
		}

		private static String Describe(String? name, ControlType? type, String? id)
		{
			List<String> parts = [];
			if (name is not null)
			{
				parts.Add("name '" + name + "'");
			}

			if (type is not null)
			{
				parts.Add("type " + type.ProgrammaticName.Replace("ControlType.", String.Empty, StringComparison.Ordinal));
			}

			if (id is not null)
			{
				parts.Add("id " + id);
			}

			return String.Join(", ", parts);
		}
	}
}