namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Documents;
	using ViewModels.ToolWindows;

	/// <summary>
	/// Shows the pieces of a command name in a <see cref="TextBlock"/>: the pieces the filter matched are bold and use the accent color of the
	/// theme, the others keep the style of the text block.
	/// </summary>
	public static class PaletteNameText
	{
		/// <summary>The pieces of the name to show (see <see cref="CommandPaletteItem.NameParts"/>).</summary>
		public static readonly DependencyProperty PartsProperty = DependencyProperty.RegisterAttached(
			"Parts",
			typeof(IReadOnlyList<PaletteTextPart>),
			typeof(PaletteNameText),
			new PropertyMetadata(null, OnPartsChanged));

		/// <summary>Gets the pieces of the name to show.</summary>
		/// <param name="element">The text block.</param>
		/// <returns>The pieces.</returns>
		public static IReadOnlyList<PaletteTextPart>? GetParts(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (IReadOnlyList<PaletteTextPart>?)element.GetValue(PartsProperty);
		}

		/// <summary>Sets the pieces of the name to show.</summary>
		/// <param name="element">The text block.</param>
		/// <param name="value">The pieces.</param>
		public static void SetParts(DependencyObject element, IReadOnlyList<PaletteTextPart>? value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(PartsProperty, value);
		}

		private static void OnPartsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not TextBlock block)
			{
				return;
			}

			block.Inlines.Clear();
			if (e.NewValue is not IReadOnlyList<PaletteTextPart> parts)
			{
				return;
			}

			foreach (PaletteTextPart part in parts)
			{
				Run run = new(part.Text);
				if (part.IsMatch)
				{
					run.FontWeight = FontWeights.Bold;
					run.SetResourceReference(TextElement.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
				}

				block.Inlines.Add(run);
			}
		}
	}
}