namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Commands;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>The command palette: a list of every command of the application that is searched while the user types.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Gets or sets where the palette reads the shortcuts of the commands (the main window sets it); the shortcuts of the editor commands are known without it.</summary>
		public ICommandGestureSource? GestureSource { get; set; }

		/// <summary>Determines whether the active editor can run a text command now.</summary>
		/// <param name="command">The text command.</param>
		/// <returns><see langword="true"/> when there is an active editor that can run it.</returns>
		public Boolean CanEditorExecute(EditorCommand command)
		{
			return this.ActiveEditor?.CanExecute(command) ?? false;
		}

		/// <summary>Opens the command palette and runs the command the user chooses (after the palette closed, so the focus is back in the editor).</summary>
		[RelayCommand]
		public async Task ShowCommandPaletteAsync()
		{
			CommandPaletteRequest request = new()
			{
				Entries = CommandCatalog.Create(this),
				RecentIds = this._settings.Recent.Commands.ToList(),
				Gestures = this.GestureSource,
			};
			if (await this._toolWindows.PickCommandAsync(request) is { } chosen)
			{
				await this.RunPaletteCommandAsync(chosen);
			}
		}

		/// <summary>Runs a command of the palette through the command the menu uses, and remembers it as recently used.</summary>
		/// <param name="entry">The command.</param>
		/// <returns><see langword="true"/> when the command was run; <see langword="false"/> when it cannot run now.</returns>
		public async Task<Boolean> RunPaletteCommandAsync(CommandEntry entry)
		{
			ArgumentNullException.ThrowIfNull(entry);

			if (!entry.CanRun)
			{
				return false;
			}

			this._settings.Recent.AddCommand(entry.Id);
			this._settings.Save();

			// The palette is closed: the focus goes back to the editor first, so a command of the editor acts where the user was.
			this.ActiveEditor?.FocusEditor();
			await this._workspace.GuardAsync(async () =>
			{
				if (entry.Command is IAsyncRelayCommand asyncCommand)
				{
					await asyncCommand.ExecuteAsync(entry.Parameter);
				}
				else
				{
					entry.Command.Execute(entry.Parameter);
				}
			});
			return true;
		}
	}
}