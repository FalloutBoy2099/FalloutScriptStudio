namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>
	/// The kinds of line of the list of declarations, which are written in groups.
	/// </summary>
	internal enum DefinitionOutputType
	{
		Nothing,
		UnknownVariable,
		GlobalVariable,
		ExportedVariable,
		ImportedVariable,
		Procedure,
		ExportedProcedure,
		ImportedProcedure
	}
}