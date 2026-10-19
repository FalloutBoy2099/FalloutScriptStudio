namespace FalloutScriptStudio.Commands
{
	/// <summary>The state a command of the palette shows next to its name.</summary>
	public enum CommandStateKind
	{
		/// <summary>A plain command: no state.</summary>
		None,

		/// <summary>A setting that is on or off.</summary>
		Toggle,

		/// <summary>One of several exclusive choices (a theme, a language, a profile): the current one is marked.</summary>
		Choice,
	}
}