namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The game variables the engine writes itself, so that a script that only reads them is correct. The list is short on purpose: it holds
	/// what the descriptions of the editor state (<c>opcodes.txt</c>: <c>load_map</c> writes the parameter to <c>GVAR_LOAD_MAP_INDEX</c>, 27)
	/// and the reputation of the player; the user adds the others in the ignore list of the settings.
	/// </summary>
	public static class GlobalEngineVariables
	{
		private static readonly HashSet<String> _names = new(StringComparer.Ordinal) { "GVAR_PLAYER_REPUTATION", "GVAR_LOAD_MAP_INDEX" };
		private static readonly HashSet<Int64> _ids = [0, 27];

		/// <summary>Determines whether the engine sets a variable.</summary>
		/// <param name="variable">The variable.</param>
		/// <returns><see langword="true"/> for a game variable that the engine writes.</returns>
		public static Boolean IsEngineProvided(GlobalVariable variable)
		{
			ArgumentNullException.ThrowIfNull(variable);
			if (variable.Kind != GlobalVariableKind.GameVariable)
			{
				return false;
			}

			if (variable.Id is { } id && _ids.Contains(id))
			{
				return true;
			}

			foreach (String name in variable.Names)
			{
				if (_names.Contains(name))
				{
					return true;
				}
			}

			return false;
		}
	}
}