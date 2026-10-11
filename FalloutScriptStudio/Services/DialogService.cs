namespace FalloutScriptStudio.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;
	using FalloutScriptStudio.Localization;
	using Microsoft.Win32;
	using Wpf.Ui.Controls;
	using MessageBox = Wpf.Ui.Controls.MessageBox;
	using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
	using TextBox = System.Windows.Controls.TextBox;

	/// <inheritdoc />
	public sealed class DialogService : IDialogService
	{
		public Task ShowInfoAsync(String title, String message)
		{
			return ShowAsync(title, message, Loc.Get("Str.Common.Ok"), null, null);
		}

		public Task ShowWarningAsync(String title, String message)
		{
			return ShowAsync(title, message, Loc.Get("Str.Common.Ok"), null, null);
		}

		public Task ShowErrorAsync(String title, String message)
		{
			return ShowAsync(title, message, Loc.Get("Str.Common.Ok"), null, null);
		}

		public async Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			return await ShowAsync(title, message, LocalizeDefault(yesText, "Yes", "Str.Common.Yes"), LocalizeDefault(noText, "No", "Str.Common.No"), null) == MessageBoxResult.Primary;
		}

		public async Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			MessageBoxResult result = await ShowAsync(title, message, LocalizeDefault(yesText, "Yes", "Str.Common.Yes"), LocalizeDefault(noText, "No", "Str.Common.No"), Loc.Get("Str.Common.Cancel"));
			return result switch
			{
				MessageBoxResult.Primary => YesNoCancel.Yes,
				MessageBoxResult.Secondary => YesNoCancel.No,
				_ => YesNoCancel.Cancel,
			};
		}

		public async Task<String?> PromptAsync(String title, String message, String defaultValue = "")
		{
			TextBox input = new TextBox
			{
				Text = defaultValue,
				MinWidth = 360,
				Margin = new Thickness(0, 8, 0, 0),
			};
			input.Loaded += (_, _) =>
			{
				input.Focus();
				input.SelectAll();
			};

			StackPanel content = new StackPanel();
			content.Children.Add(new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
			content.Children.Add(input);

			MessageBox box = CreateBox(title, content, Loc.Get("Str.Common.Ok"), null, Loc.Get("Str.Common.Cancel"));
			MessageBoxResult result = await box.ShowDialogAsync();
			return result == MessageBoxResult.Primary ? input.Text : null;
		}

		public String? PickOpenFile(String title, String filter, String? initialDirectory = null)
		{
			OpenFileDialog dialog = new OpenFileDialog { Title = title, Filter = filter, InitialDirectory = initialDirectory ?? String.Empty };
			return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
		}

		public IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null)
		{
			OpenFileDialog dialog = new OpenFileDialog
			{
				Title = title,
				Filter = filter,
				Multiselect = true,
				InitialDirectory = initialDirectory ?? String.Empty,
			};
			return dialog.ShowDialog(GetOwner()) == true ? dialog.FileNames.ToList() : new List<String>();
		}

		public String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null)
		{
			SaveFileDialog dialog = new SaveFileDialog
			{
				Title = title,
				Filter = filter,
				InitialDirectory = initialDirectory ?? String.Empty,
				FileName = fileName ?? String.Empty,
			};
			return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
		}

		public String? PickFolder(String title, String? initialDirectory = null)
		{
			OpenFolderDialog dialog = new OpenFolderDialog { Title = title, InitialDirectory = initialDirectory ?? String.Empty };
			return dialog.ShowDialog(GetOwner()) == true ? dialog.FolderName : null;
		}

		/// <summary>Replaces the English default caption of a button by its translation; a caption chosen by the caller is kept.</summary>
		private static String LocalizeDefault(String caption, String english, String key)
		{
			return String.Equals(caption, english, StringComparison.Ordinal) ? Loc.Get(key) : caption;
		}

		private static Window? GetOwner()
		{
			Application? app = Application.Current;
			if (app is null)
			{
				return null;
			}

			return app.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? app.MainWindow;
		}

		private static MessageBox CreateBox(String title, Object content, String? primary, String? secondary, String? close)
		{
			MessageBox box = new MessageBox
			{
				Title = title,
				Content = content,
				PrimaryButtonText = primary ?? String.Empty,
				SecondaryButtonText = secondary ?? String.Empty,
				CloseButtonText = close ?? String.Empty,
				Owner = GetOwner(),
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
			};

			// WPF-UI keeps a button without a caption on screen as an empty box: hide the ones that are not used.
			box.Loaded += (_, _) => HideEmptyButtons(box);
			return box;
		}

		private static void HideEmptyButtons(DependencyObject root)
		{
			for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				if (child is Wpf.Ui.Controls.Button button && button.Icon is null && button.Content is String { Length: 0 })
				{
					button.Visibility = Visibility.Collapsed;
				}

				HideEmptyButtons(child);
			}
		}

		private static async Task<MessageBoxResult> ShowAsync(String title, String message, String primary, String? secondary, String? close)
		{
			Object content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 };
			return await CreateBox(title, content, primary, secondary, close).ShowDialogAsync();
		}
	}
}
