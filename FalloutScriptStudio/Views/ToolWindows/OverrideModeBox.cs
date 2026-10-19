namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows.Controls;

	/// <summary>
	/// The drop-down list "Inherit global / Override" of an option of the project settings. The two entries follow the language of the
	/// interface; <see cref="System.Windows.Controls.Primitives.Selector.SelectedIndex"/> is 0 for "inherit" and 1 for "override".
	/// </summary>
	public sealed class OverrideModeBox : ComboBox
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="OverrideModeBox"/> class.
		/// </summary>
		public OverrideModeBox()
		{
			this.Width = 160;
			this.Items.Add(CreateItem("Str.ToolWindows.Project.Inherit"));
			this.Items.Add(CreateItem("Str.ToolWindows.Project.Override"));
		}

		private static ComboBoxItem CreateItem(String resourceKey)
		{
			ComboBoxItem item = new();
			item.SetResourceReference(ContentControl.ContentProperty, resourceKey);
			return item;
		}
	}
}