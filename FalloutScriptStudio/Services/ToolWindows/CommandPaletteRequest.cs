namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using Commands;

	/// <summary>What the command palette has to show.</summary>
	public sealed record CommandPaletteRequest
	{
		/// <summary>Gets the commands to list, in menu order.</summary>
		public required IReadOnlyList<CommandEntry> Entries { get; init; }

		/// <summary>Gets the identifiers of the commands used last, the newest at the end (the order of the list of recent commands).</summary>
		public IReadOnlyList<String> RecentIds { get; init; } = Array.Empty<String>();

		/// <summary>Gets where the shortcuts of the commands are read, or <see langword="null"/> to show only the shortcuts of the editor commands.</summary>
		public ICommandGestureSource? Gestures { get; init; }
	}
}