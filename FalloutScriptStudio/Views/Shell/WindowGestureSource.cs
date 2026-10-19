namespace FalloutScriptStudio.Views.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Input;
	using Commands;

	/// <summary>
	/// Tells the command palette the shortcut of a command from what the main window really has: the text a menu item shows (the same as the
	/// binding it documents, <c>MainWindowGestureTests</c> checks that) and the key bindings of the window. The table is read when the first
	/// shortcut is asked for.
	/// </summary>
	public sealed class WindowGestureSource : ICommandGestureSource
	{
		private readonly InputBindingCollection _bindings;
		private readonly ItemsControl _menu;
		private List<(ICommand Command, Object? Parameter, String Text)>? _byCommand;
		private Dictionary<String, String>? _byProperty;

		/// <summary>Initializes a new instance of the <see cref="WindowGestureSource"/> class.</summary>
		/// <param name="bindings">The key bindings of the window.</param>
		/// <param name="menu">The menu bar of the window.</param>
		public WindowGestureSource(InputBindingCollection bindings, ItemsControl menu)
		{
			ArgumentNullException.ThrowIfNull(bindings);
			ArgumentNullException.ThrowIfNull(menu);
			this._bindings = bindings;
			this._menu = menu;
		}

		/// <inheritdoc />
		public String? GetGesture(CommandEntry entry)
		{
			ArgumentNullException.ThrowIfNull(entry);
			this.EnsureTable();

			if (entry.Property is { } property)
			{
				return this._byProperty!.TryGetValue(property, out String? text) ? text : null;
			}

			foreach ((ICommand command, Object? parameter, String text) in this._byCommand!)
			{
				if (ReferenceEquals(command, entry.Command) && Equals(parameter, entry.Parameter))
				{
					return text;
				}
			}

			return null;
		}

		private void EnsureTable()
		{
			if (this._byCommand is not null)
			{
				return;
			}

			List<(ICommand, Object?, String)> byCommand = [];
			Dictionary<String, String> byProperty = new(StringComparer.Ordinal);
			ReadMenu(this._menu, byCommand, byProperty);

			// The key bindings that no menu item documents (the ones of the document tabs, for example).
			foreach (InputBinding binding in this._bindings.OfType<InputBinding>())
			{
				if (binding.Command is { } command && binding.Gesture is KeyGesture gesture)
				{
					byCommand.Add((command, binding.CommandParameter, GestureText.Format(gesture)));
				}
			}

			this._byProperty = byProperty;
			this._byCommand = byCommand;
		}

		private static void ReadMenu(ItemsControl parent, List<(ICommand, Object?, String)> byCommand, Dictionary<String, String> byProperty)
		{
			foreach (Object child in parent.Items)
			{
				if (child is not MenuItem item)
				{
					continue;
				}

				if (!String.IsNullOrWhiteSpace(item.InputGestureText))
				{
					if (item.Command is { } command)
					{
						byCommand.Add((command, item.CommandParameter, item.InputGestureText));
					}
					else if (BindingOperations.GetBinding(item, MenuItem.IsCheckedProperty)?.Path?.Path is { Length: > 0 } property)
					{
						byProperty[property] = item.InputGestureText;
					}
				}

				ReadMenu(item, byCommand, byProperty);
			}
		}
	}
}