namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Windows;
	using System.Windows.Automation;
	using ICSharpCode.AvalonEdit;

	/// <summary>
	/// Gives a code editor an accessible name that follows the language. The keyboard focus lives in the text area inside the editor, and a
	/// screen reader announces the name of the control that has the focus, so the name is set on the text area as well.
	/// </summary>
	public static class EditorAutomation
	{
		/// <summary>Identifies the NameResource attached property: the key of the localized accessible name.</summary>
		public static readonly DependencyProperty NameResourceProperty = DependencyProperty.RegisterAttached(
			"NameResource",
			typeof(String),
			typeof(EditorAutomation),
			new PropertyMetadata(null, OnNameResourceChanged));

		/// <summary>Gets the resource key of the accessible name of the editor.</summary>
		/// <param name="element">The editor.</param>
		/// <returns>The resource key, or <see langword="null"/>.</returns>
		public static String? GetNameResource(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (String?)element.GetValue(NameResourceProperty);
		}

		/// <summary>Sets the resource key of the accessible name of the editor.</summary>
		/// <param name="element">The editor.</param>
		/// <param name="value">The resource key.</param>
		public static void SetNameResource(DependencyObject element, String? value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(NameResourceProperty, value);
		}

		private static void OnNameResourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not TextEditor editor || e.NewValue is not String key)
			{
				return;
			}

			editor.SetResourceReference(AutomationProperties.NameProperty, key);
			editor.TextArea.SetResourceReference(AutomationProperties.NameProperty, key);
		}
	}
}
