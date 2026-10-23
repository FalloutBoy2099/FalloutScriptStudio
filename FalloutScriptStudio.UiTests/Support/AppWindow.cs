namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Automation;

	/// <summary>A top-level window of the program under test.</summary>
	internal sealed class AppWindow
	{
		/// <summary>Initializes a new instance of the <see cref="AppWindow"/> class.</summary>
		/// <param name="element">The window element.</param>
		/// <param name="handle">The window handle.</param>
		public AppWindow(AutomationElement element, IntPtr handle)
		{
			this.Element = element;
			this.Handle = handle;
		}

		/// <summary>Gets the UI Automation element of the window.</summary>
		public AutomationElement Element { get; }

		/// <summary>Gets the window handle.</summary>
		public IntPtr Handle { get; }

		/// <summary>Gets the title of the window.</summary>
		public String Title => this.Element.Current.Name;

		/// <summary>Gets the bounds of the window in device pixels.</summary>
		public Rect Bounds => this.Element.Current.BoundingRectangle;

		/// <summary>Finds an element of the window by name, type or automation id.</summary>
		/// <param name="name">The exact name, or <see langword="null"/> for any.</param>
		/// <param name="type">The control type, or <see langword="null"/> for any.</param>
		/// <param name="id">The automation id, or <see langword="null"/> for any.</param>
		/// <returns>The element, or <see langword="null"/>.</returns>
		public AutomationElement? Find(String? name = null, ControlType? type = null, String? id = null)
		{
			return Uia.Find(this.Element, name, type, id);
		}

		/// <summary>Waits for an element of the window.</summary>
		/// <param name="name">The exact name, or <see langword="null"/> for any.</param>
		/// <param name="type">The control type, or <see langword="null"/> for any.</param>
		/// <param name="id">The automation id, or <see langword="null"/> for any.</param>
		/// <param name="timeout">The time limit.</param>
		/// <returns>The element.</returns>
		public AutomationElement WaitFor(String? name = null, ControlType? type = null, String? id = null, TimeSpan? timeout = null)
		{
			return Uia.WaitFor(this.Element, name, type, id, timeout);
		}

		/// <summary>Gets the elements of the window with their properties.</summary>
		/// <returns>The elements.</returns>
		public IReadOnlyList<UiNode> Snapshot()
		{
			return Uia.Snapshot(this.Element);
		}

		/// <summary>Gets the texts the window shows (names of text elements, buttons, headers...), in tree order.</summary>
		/// <returns>The texts.</returns>
		public IReadOnlyList<String> Texts()
		{
			return this.Snapshot()
				.Where(node => !node.IsOffscreen && node.Name.Length > 0)
				.Select(node => node.Name)
				.ToList();
		}

		/// <summary>Finds the elements that stick out of the window: visible elements whose rectangle is not inside the window (a clipped control).</summary>
		/// <returns>The descriptions of the elements.</returns>
		public IReadOnlyList<String> FindClippedElements()
		{
			Rect window = this.Bounds;
			Rect allowed = new(window.X - 2, window.Y - 2, window.Width + 4, window.Height + 4);
			List<String> clipped = [];
			foreach (UiNode node in this.Snapshot())
			{
				if (node.IsOffscreen || node.Bounds.IsEmpty || node.Bounds.Width <= 0 || node.Bounds.Height <= 0)
				{
					continue;
				}

				if (node.Type == ControlType.Window || node.Type == ControlType.ScrollBar || node.Type == ControlType.Thumb)
				{
					continue;
				}

				if (!allowed.Contains(node.Bounds) && !HasScrollingAncestor(node.Element))
				{
					clipped.Add(node.ToString());
				}
			}

			return clipped;
		}

		private static Boolean HasScrollingAncestor(AutomationElement element)
		{
			// A scroll viewer clips what does not fit on purpose (the headers of a tab strip that scrolls, a long list).
			try
			{
				AutomationElement? parent = TreeWalker.RawViewWalker.GetParent(element);
				while (parent is not null)
				{
					if (parent.TryGetCurrentPattern(ScrollPattern.Pattern, out _))
					{
						return true;
					}

					parent = TreeWalker.RawViewWalker.GetParent(parent);
				}
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or System.Runtime.InteropServices.COMException)
			{
				// The element vanished: it does not stick out any more.
			}

			return false;
		}

		/// <summary>Closes the window through its window pattern.</summary>
		public void Close()
		{
			Uia.CloseWindow(this.Element);
		}
	}
}