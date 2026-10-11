namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows;
	using System.Windows.Controls;

	/// <summary>
	/// A row of the settings window: a title with an optional description on the left and the editing control (the content) on the right.
	/// The look is defined in <c>SettingsWindow.xaml</c>.
	/// </summary>
	public sealed class SettingCard : ContentControl
	{
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
	}
}
