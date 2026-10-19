namespace FalloutScriptStudio.Tests.Commands
{
	using System;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Input;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Commands;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Views.Shell;
	using Xunit;

	/// <summary>The shortcuts the palette shows come from the menu and the key bindings; built here from a menu that is never shown.</summary>
	public sealed class WindowGestureSourceTests
	{
		private static CommandEntry Entry(ICommand command, Object? parameter = null, String? property = null)
		{
			return new CommandEntry("x", Array.Empty<CommandText>(), CommandText.FromLiteral("x"), command, parameter, () => CommandAvailability.Available, property is null ? null : () => false, false, property);
		}

		[Fact]
		public void TheTextOfAMenuItemIsTheShortcutOfItsCommand_AndTheParameterTellsTheItemsApart()
		{
			ShellStaRunner.Run(() =>
			{
				RelayCommand<EditorCommand> edit = new RelayCommand<EditorCommand>(_ => { });
				RelayCommand save = new RelayCommand(() => { });
				Menu menu = new Menu();
				MenuItem file = new MenuItem { Header = "File" };
				file.Items.Add(new MenuItem { Command = save, InputGestureText = "Ctrl+S" });
				MenuItem code = new MenuItem { Header = "Code" };
				code.Items.Add(new MenuItem { Command = edit, CommandParameter = EditorCommand.CommentSelection, InputGestureText = "Alt+C" });
				code.Items.Add(new MenuItem { Command = edit, CommandParameter = EditorCommand.UncommentSelection, InputGestureText = "Alt+X" });
				code.Items.Add(new MenuItem { Command = edit, CommandParameter = EditorCommand.FormatDocument });
				menu.Items.Add(file);
				menu.Items.Add(code);
				WindowGestureSource source = new WindowGestureSource(new Grid().InputBindings, menu);

				Assert.Equal("Ctrl+S", source.GetGesture(Entry(save)));
				Assert.Equal("Alt+C", source.GetGesture(Entry(edit, EditorCommand.CommentSelection)));
				Assert.Equal("Alt+X", source.GetGesture(Entry(edit, EditorCommand.UncommentSelection)));
				Assert.Null(source.GetGesture(Entry(edit, EditorCommand.FormatDocument)));
				Assert.Null(source.GetGesture(Entry(new RelayCommand(() => { }))));
				return System.Threading.Tasks.Task.CompletedTask;
			});
		}

		[Fact]
		public void ACheckItemIsFoundByTheSettingItChanges()
		{
			ShellStaRunner.Run(() =>
			{
				Menu menu = new Menu();
				MenuItem view = new MenuItem { Header = "View" };
				MenuItem right = new MenuItem { Header = "Right browser", IsCheckable = true, InputGestureText = "Alt+R" };
				BindingOperations.SetBinding(right, MenuItem.IsCheckedProperty, new Binding("ShowRightBrowser"));
				MenuItem plain = new MenuItem { Header = "Line numbers", IsCheckable = true };
				BindingOperations.SetBinding(plain, MenuItem.IsCheckedProperty, new Binding("ShowLineNumbers"));
				view.Items.Add(right);
				view.Items.Add(plain);
				menu.Items.Add(view);
				WindowGestureSource source = new WindowGestureSource(new Grid().InputBindings, menu);

				Assert.Equal("Alt+R", source.GetGesture(Entry(new RelayCommand(() => { }), property: "ShowRightBrowser")));
				Assert.Null(source.GetGesture(Entry(new RelayCommand(() => { }), property: "ShowLineNumbers")));
				return System.Threading.Tasks.Task.CompletedTask;
			});
		}

		[Fact]
		public void AKeyBindingNoMenuItemDocumentsIsFoundToo()
		{
			ShellStaRunner.Run(() =>
			{
				RelayCommand next = new RelayCommand(() => { });
				Grid host = new Grid();
				host.InputBindings.Add(new KeyBinding(next, new KeyGesture(Key.Tab, ModifierKeys.Control)));
				WindowGestureSource source = new WindowGestureSource(host.InputBindings, new Menu());

				Assert.Equal("Ctrl+Tab", source.GetGesture(Entry(next)));
				return System.Threading.Tasks.Task.CompletedTask;
			});
		}

		[Theory]
		[InlineData(Key.S, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+S")]
		[InlineData(Key.OemPlus, ModifierKeys.Control, "Ctrl++")]
		[InlineData(Key.OemMinus, ModifierKeys.Control, "Ctrl+-")]
		[InlineData(Key.D0, ModifierKeys.Control, "Ctrl+0")]
		[InlineData(Key.Cancel, ModifierKeys.Control, "Ctrl+Break")]
		[InlineData(Key.OemPeriod, ModifierKeys.Control, "Ctrl+.")]
		[InlineData(Key.Divide, ModifierKeys.Control, "Ctrl+/")]
		[InlineData(Key.F8, ModifierKeys.Alt | ModifierKeys.Control, "Ctrl+Alt+F8")]
		[InlineData(Key.F1, ModifierKeys.None, "F1")]
		public void GesturesAreWrittenLikeTheMenusWriteThem(Key key, ModifierKeys modifiers, String expected)
		{
			Assert.Equal(expected, GestureText.Format(key, modifiers));
		}

		[Fact]
		public void SeveralGesturesAreJoinedLikeTheMenuDoes()
		{
			String? text = GestureText.FormatAll(new InputGesture[] { new KeyGesture(Key.Space, ModifierKeys.Control), new KeyGesture(Key.Space, ModifierKeys.Alt) });

			Assert.Equal("Ctrl+Space / Alt+Space", text);
			Assert.Null(GestureText.FormatAll(Array.Empty<InputGesture>()));
		}
	}
}