namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>
	/// The project settings window: for each option a project can override, the choice between the global setting and a value of the project.
	/// </summary>
	public partial class ProjectSettingsWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectSettingsWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public ProjectSettingsWindow(ProjectSettingsViewModel viewModel)
		{
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
			this.Loaded += (_, _) => this.ShowOption(viewModel.FocusOptionId);
		}

		/// <summary>Scrolls to the card of an option and puts the focus on its first control.</summary>
		/// <param name="optionId">The option (see <c>ProjectOptions</c>), or <see langword="null"/> to stay at the top.</param>
		/// <returns><see langword="true"/> when the option was found.</returns>
		public Boolean ShowOption(String? optionId)
		{
			if (String.IsNullOrEmpty(optionId) || FindOption(this, optionId) is not { } target)
			{
				return false;
			}

			target.BringIntoView();
			_ = target.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			return true;
		}

		private static FrameworkElement? FindOption(DependencyObject root, String optionId)
		{
			foreach (Object child in LogicalTreeHelper.GetChildren(root))
			{
				if (child is not DependencyObject element)
				{
					continue;
				}

				if (element is SettingCard card && String.Equals(card.OptionId, optionId, StringComparison.Ordinal))
				{
					return card;
				}

				if (element is FrameworkElement { Tag: String tag } framework && String.Equals(tag, optionId, StringComparison.Ordinal))
				{
					return framework;
				}

				if (FindOption(element, optionId) is { } found)
				{
					return found;
				}
			}

			return null;
		}
	}
}