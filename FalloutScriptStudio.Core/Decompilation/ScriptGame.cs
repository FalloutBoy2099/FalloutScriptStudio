namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>Which game a script was made for: the opcodes of the two games are not quite the same.</summary>
	public enum ScriptGame
	{
		/// <summary>Fallout 1.</summary>
		Fallout1,

		/// <summary>Fallout 2, and what the Fallout 2 engine mods (sfall) add.</summary>
		Fallout2,
	}
}
