namespace FalloutScriptStudio.Commands
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// One command of the table of the catalog: where it is in the menu (the localization keys of the menus above it and of its own header),
	/// which command of the main view model it runs and when it is enabled. Everything else (the name, the shortcut, the state) is read when the
	/// palette opens.
	/// </summary>
	/// <param name="MenuKeys">The keys of the menu headers above the item, outermost first (<c>Str.Shell.Palette.Menu.Code</c>).</param>
	/// <param name="TitleKey">The key of the header of the item.</param>
	/// <param name="CommandPath">The property of the main view model that holds the command (<c>SaveCommand</c>, <c>Output.NextProblemCommand</c>), or <see langword="null"/> for a toggle.</param>
	/// <param name="Parameter">The command parameter, or <see langword="null"/>.</param>
	/// <param name="Requirement">When the command is enabled.</param>
	/// <param name="TogglePath">The Boolean property of the main view model that a check item changes, or <see langword="null"/> for a command.</param>
	public sealed record CommandDefinition(
		IReadOnlyList<String> MenuKeys,
		String TitleKey,
		String? CommandPath,
		Object? Parameter,
		CommandRequirement Requirement,
		String? TogglePath = null)
	{
		/// <summary>Gets the stable identifier of the command (what the list of recent commands remembers).</summary>
		public String Id => this.CommandPath is null
			? "toggle:" + this.TogglePath
			: this.Parameter is null ? this.CommandPath : this.CommandPath + ":" + this.Parameter;
	}
}