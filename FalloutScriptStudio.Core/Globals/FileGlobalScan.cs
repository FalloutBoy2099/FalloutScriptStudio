namespace FalloutScriptStudio.Core.Globals
{
	using System;

	/// <summary>What the scan of one file found about global variables: the uses, the procedures they are in and the macros that stand for a number or a string.</summary>
	internal sealed class FileGlobalScan
	{
		internal FileGlobalScan(RawGlobalUse[] uses, String[] procedures, MacroConstant[] constants)
		{
			this.Uses = uses;
			this.Procedures = procedures;
			this.Constants = constants;
		}

		/// <summary>Gets an empty result.</summary>
		internal static FileGlobalScan Empty { get; } = new FileGlobalScan(Array.Empty<RawGlobalUse>(), Array.Empty<String>(), Array.Empty<MacroConstant>());

		/// <summary>Gets the uses in text order.</summary>
		internal RawGlobalUse[] Uses { get; }

		/// <summary>Gets the names of the procedures (or of the macros) the uses are in.</summary>
		internal String[] Procedures { get; }

		/// <summary>Gets the constants the file defines.</summary>
		internal MacroConstant[] Constants { get; }

		/// <summary>Gets a value indicating whether there is nothing in the result.</summary>
		internal Boolean IsEmpty => this.Uses.Length == 0 && this.Constants.Length == 0;
	}
}