namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;

	/// <summary>The names of the variables a snippet body can use (<c>$NAME</c> or <c>${NAME}</c>).</summary>
	public static class SnippetVariables
	{
		/// <summary>The procedure the caret is in (empty outside a procedure).</summary>
		public const String CurrentProcedure = "CURRENT_PROCEDURE";

		/// <summary>The name of the file with its extension (empty for a document that was never saved).</summary>
		public const String FileName = "FILE_NAME";

		/// <summary>The name of the script: the file name without its extension.</summary>
		public const String ScriptName = "SCRIPT_NAME";

		/// <summary>Today's date as <c>yyyy-MM-dd</c>.</summary>
		public const String Date = "DATE";

		/// <summary>The time as <c>HH:mm:ss</c>.</summary>
		public const String Time = "TIME";

		/// <summary>The selected text (empty when nothing is selected): a snippet that uses it can surround code.</summary>
		public const String Selection = "SELECTION";

		private static readonly String[] _all = { CurrentProcedure, FileName, ScriptName, Date, Time, Selection };

		/// <summary>Gets every variable name.</summary>
		public static IReadOnlyList<String> All => _all;

		/// <summary>Determines whether a name is a variable.</summary>
		/// <param name="name">The name (case-sensitive: the variables are written in capitals).</param>
		/// <returns><see langword="true"/> for a variable.</returns>
		public static Boolean IsKnown(String name)
		{
			return Array.IndexOf(_all, name) >= 0;
		}
	}
}