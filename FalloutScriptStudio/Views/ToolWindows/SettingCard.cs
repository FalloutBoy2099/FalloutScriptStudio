namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Data;

	/// <summary>
	/// A row of the settings window: a title with an optional description on the left and the editing control (the content) on the right.
	/// The look is defined in <c>SettingsWindow.xaml</c>. The title is also the accessible name of the editing controls of the row, so a
	/// screen reader announces "Output folder, edit" and not only the type of the control.
	/// </summary>
	[TemplatePart(Name = TitlePartName, Type = typeof(FrameworkElement))]
	public sealed class SettingCard : ContentControl
	{
		/// <summary>The name of the element of the template that shows the title.</summary>
		public const String TitlePartName = "PART_Title";

		/// <summary>Identifies the <see cref="Title"/> property.</summary>
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
			nameof(Title),
			typeof(String),
			typeof(SettingCard),
			new PropertyMetadata(String.Empty));

		/// <summary>Identifies the <see cref="Description"/> property.</summary>
		public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
			nameof(Description),
			typeof(String),
			typeof(SettingCard),
			new PropertyMetadata(String.Empty));

		private FrameworkElement? _titleElement;

		/// <summary>Gets or sets the title of the setting.</summary>
		public String Title
		{
			get => (String)this.GetValue(TitleProperty);
			set => this.SetValue(TitleProperty, value);
		}

		/// <summary>Gets or sets the explanation shown under the title.</summary>
		public String Description
		{
			get => (String)this.GetValue(DescriptionProperty);
			set => this.SetValue(DescriptionProperty, value);
		}

		/// <inheritdoc/>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			this._titleElement = this.GetTemplateChild(TitlePartName) as FrameworkElement;
			this.LabelContent();
		}

		/// <inheritdoc/>
		protected override void OnContentChanged(Object oldContent, Object newContent)
		{
			base.OnContentChanged(oldContent, newContent);
			this.LabelContent();
		}

		/// <summary>Controls that edit a value and have no text of their own; buttons keep their own name.</summary>
		private static Boolean NeedsLabel(DependencyObject element)
		{
			return element is TextBoxBase or PasswordBox or Selector or RangeBase or Wpf.Ui.Controls.NumberBox
				|| (element is ToggleButton { Content: null });
		}

		private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
		{
			yield return root;
			foreach (Object child in LogicalTreeHelper.GetChildren(root))
			{
				if (child is DependencyObject element)
				{
					foreach (DependencyObject descendant in Descendants(element))
					{
						yield return descendant;
					}
				}
			}
		}

		private void LabelContent()
		{
			if (this._titleElement is null || this.Content is not DependencyObject content)
			{
				return;
			}

			foreach (DependencyObject element in Descendants(content))
			{
				if (!NeedsLabel(element)
					|| AutomationProperties.GetLabeledBy(element) is not null
					|| !String.IsNullOrEmpty(AutomationProperties.GetName(element)))
				{
					continue;
				}

				AutomationProperties.SetLabeledBy(element, this._titleElement);
				if (String.IsNullOrEmpty(AutomationProperties.GetHelpText(element)) && !BindingOperations.IsDataBound(element, AutomationProperties.HelpTextProperty))
				{
					BindingOperations.SetBinding(element, AutomationProperties.HelpTextProperty, new Binding(nameof(this.Description)) { Source = this });
				}
			}
		}
	}
}
