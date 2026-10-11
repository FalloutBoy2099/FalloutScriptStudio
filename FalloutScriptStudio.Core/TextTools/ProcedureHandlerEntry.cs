namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// One entry of the menu of standard procedure handlers (<c>prochandlers.ini</c>).
	/// </summary>
	/// <param name="Kind">What the entry is.</param>
	/// <param name="Name">For a handler the procedure name (it may include an argument list, for example <c>Example(arg1, arg2)</c>); for a group its title.</param>
	/// <param name="Description">The tool tip text (empty when there is none).</param>
	/// <param name="Children">The entries of a group; empty for the other kinds.</param>
	public sealed record ProcedureHandlerEntry(ProcedureHandlerKind Kind, String Name, String Description, IReadOnlyList<ProcedureHandlerEntry> Children)
	{
		/// <summary>Creates a handler entry.</summary>
		/// <param name="name">The procedure name.</param>
		/// <param name="description">The description.</param>
		/// <returns>The entry.</returns>
		public static ProcedureHandlerEntry CreateHandler(String name, String description)
		{
			return new ProcedureHandlerEntry(ProcedureHandlerKind.Handler, name, description, Array.Empty<ProcedureHandlerEntry>());
		}

		/// <summary>Creates a separator entry.</summary>
		/// <returns>The entry.</returns>
		public static ProcedureHandlerEntry CreateSeparator()
		{
			return new ProcedureHandlerEntry(ProcedureHandlerKind.Separator, String.Empty, String.Empty, Array.Empty<ProcedureHandlerEntry>());
		}
	}
}
