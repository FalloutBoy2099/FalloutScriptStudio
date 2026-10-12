namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Automation.Peers;

	/// <summary>
	/// Makes a control whose content changes on its own (the status bar) a polite live region: a screen reader announces the new
	/// text without moving the keyboard focus. Bind <see cref="TextProperty"/> to the same value the control shows.
	/// </summary>
	public static class LiveRegion
	{
		/// <summary>Identifies the Text attached property: the text announced when it changes.</summary>
		public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
			"Text",
			typeof(String),
			typeof(LiveRegion),
			new PropertyMetadata(null, OnTextChanged));

		/// <summary>Gets the announced text of the element.</summary>
		/// <param name="element">The element.</param>
		/// <returns>The text, or <see langword="null"/>.</returns>
		public static String? GetText(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (String?)element.GetValue(TextProperty);
		}

		/// <summary>Sets the announced text of the element.</summary>
		/// <param name="element">The element.</param>
		/// <param name="value">The text.</param>
		public static void SetText(DependencyObject element, String? value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(TextProperty, value);
		}

		private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			AutomationProperties.SetLiveSetting(d, AutomationLiveSetting.Polite);
			if (d is UIElement element && e.OldValue is not null && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
			{
				UIElementAutomationPeer.CreatePeerForElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
			}
		}
	}
}
