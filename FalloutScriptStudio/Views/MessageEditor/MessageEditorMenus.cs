namespace FalloutScriptStudio.Views.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using Core.Configuration;
	using Localization;
	using ViewModels.MessageEditor;

	/// <summary>
	/// The context menus of the Message Editor: completes the menus declared in XAML with the items that are built in code
	/// (comment colors, font sizes, recent files), keeps their texts localized and shows them under the toolbar buttons.
	/// </summary>
	internal sealed class MessageEditorMenus
	{
		private static readonly String[] MenuKeys = { "RowMenu", "OptionsMenu", "OpenWithMenu" };

		private readonly ResourceDictionary _resources;
		private readonly MessageEditorViewModel _viewModel;
		private readonly ButtonBase _recentButton;
		private readonly ButtonBase _openWithButton;
		private readonly ButtonBase _optionsButton;
		private readonly List<(MenuItem Item, CommentHighlightColor Color)> _colorItems = [];
		private readonly List<(MenuItem Item, Int32 Points)> _fontItems = [];

		/// <summary>Initializes the menus.</summary>
		/// <param name="resources">The resources of the window that declare <c>RowMenu</c>, <c>OptionsMenu</c> and <c>OpenWithMenu</c>.</param>
		/// <param name="viewModel">The view model of the window.</param>
		/// <param name="recentButton">The button that shows the list of recent files.</param>
		/// <param name="openWithButton">The button that shows the "open with" menu.</param>
		/// <param name="optionsButton">The button that shows the options menu.</param>
		public MessageEditorMenus(ResourceDictionary resources, MessageEditorViewModel viewModel, ButtonBase recentButton, ButtonBase openWithButton, ButtonBase optionsButton)
		{
			ArgumentNullException.ThrowIfNull(resources);
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(recentButton);
			ArgumentNullException.ThrowIfNull(openWithButton);
			ArgumentNullException.ThrowIfNull(optionsButton);
			this._resources = resources;
			this._viewModel = viewModel;
			this._recentButton = recentButton;
			this._openWithButton = openWithButton;
			this._optionsButton = optionsButton;
		}

		/// <summary>Gives the menus their data context, builds the code-made items and connects the toolbar buttons.</summary>
		public void Attach()
		{
			foreach (String key in MenuKeys)
			{
				this.GetMenu(key).DataContext = this._viewModel;
			}

			this.BuildOptionItems();
			this._recentButton.Click += (_, _) => this.ShowRecent();
			this._openWithButton.Click += (_, _) => ShowMenu(this.GetMenu("OpenWithMenu"), this._openWithButton);
			this._optionsButton.Click += (_, _) => ShowMenu(this.GetMenu("OptionsMenu"), this._optionsButton);
		}

		/// <summary>
		/// Sets the texts that cannot follow a <c>DynamicResource</c>: the menu items that are not in the element tree while the menu is closed,
		/// and the items made in code.
		/// </summary>
		public void RefreshTexts()
		{
			foreach (String key in MenuKeys)
			{
				RefreshMenuTexts(this.GetMenu(key).Items);
			}

			foreach ((MenuItem item, CommentHighlightColor color) in this._colorItems)
			{
				item.Header = DescribeColor(color);
			}

			foreach ((MenuItem item, Int32 points) in this._fontItems)
			{
				item.Header = Loc.Format("Str.MessageEditor.Menu.FontPoints", points);
			}
		}

		/// <summary>
		/// Makes the menu items that are not in the element tree (a closed context menu) read their <c>DynamicResource</c> texts again.
		/// </summary>
		private static void RefreshMenuTexts(ItemCollection items)
		{
			foreach (MenuItem item in items.OfType<MenuItem>())
			{
				item.InvalidateProperty(HeaderedItemsControl.HeaderProperty);
				item.InvalidateProperty(FrameworkElement.ToolTipProperty);
				RefreshMenuTexts(item.Items);
			}
		}

		private static String DescribeColor(CommentHighlightColor color)
		{
			return color switch
			{
				CommentHighlightColor.PaleGreen => Loc.Get("Str.MessageEditor.Color.PaleGreen"),
				CommentHighlightColor.Lavender => Loc.Get("Str.MessageEditor.Color.Lavender"),
				_ => Loc.Get("Str.MessageEditor.Color.LightYellow"),
			};
		}

		private static void ShowMenu(ContextMenu menu, FrameworkElement target)
		{
			menu.PlacementTarget = target;
			menu.Placement = PlacementMode.Bottom;
			menu.IsOpen = true;
		}

		private ContextMenu GetMenu(String key)
		{
			return (ContextMenu)this._resources[key];
		}

		private void BuildOptionItems()
		{
			ContextMenu options = this.GetMenu("OptionsMenu");
			MenuItem colors = options.Items.OfType<MenuItem>().First(item => Equals(item.Tag, "colors"));
			foreach (CommentHighlightColor color in this._viewModel.CommentColors)
			{
				MenuItem item = new()
				{ Tag = color };
				this._colorItems.Add((item, color));
				item.Click += (_, _) => this._viewModel.SetCommentColorCommand.Execute(color);
				colors.Items.Add(item);
			}

			colors.SubmenuOpened += (_, _) =>
			{
				foreach (MenuItem item in colors.Items.OfType<MenuItem>())
				{
					item.IsChecked = Equals(item.Tag, this._viewModel.CommentColor);
				}
			};

			MenuItem fonts = options.Items.OfType<MenuItem>().First(item => Equals(item.Tag, "fonts"));
			for (Int32 index = 0; index < this._viewModel.FontSizes.Count; index++)
			{
				Int32 captured = index;
				MenuItem item = new()
				{ Tag = index };
				this._fontItems.Add((item, this._viewModel.FontSizes[index]));
				item.Click += (_, _) => this._viewModel.SetFontSizeIndexCommand.Execute(captured);
				fonts.Items.Add(item);
			}

			fonts.SubmenuOpened += (_, _) =>
			{
				foreach (MenuItem item in fonts.Items.OfType<MenuItem>())
				{
					item.IsChecked = Equals(item.Tag, this._viewModel.FontSizeIndex);
				}
			};
		}

		private void ShowRecent()
		{
			ContextMenu menu = new();
			if (this._viewModel.RecentFiles.Count == 0)
			{
				menu.Items.Add(new MenuItem { Header = Loc.Get("Str.MessageEditor.Menu.NoRecent"), IsEnabled = false });
			}

			foreach (String path in this._viewModel.RecentFiles)
			{
				String captured = path;
				MenuItem item = new()
				{ Header = path.Replace("_", "__", StringComparison.Ordinal) };
				item.Click += (_, _) => this._viewModel.OpenRecentCommand.Execute(captured);
				menu.Items.Add(item);
			}

			ShowMenu(menu, this._recentButton);
		}
	}
}