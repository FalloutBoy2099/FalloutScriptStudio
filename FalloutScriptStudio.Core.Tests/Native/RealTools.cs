namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;

	/// <summary>The external tools a test needs.</summary>
	[Flags]
	internal enum RealTools
	{
		None = 0,

		/// <summary>parser.dll plus the built parser host.</summary>
		Parser = 1,

		/// <summary>compile.exe.</summary>
		Compiler = 2,

		/// <summary>int2ssl.exe.</summary>
		Decompiler = 4,

		/// <summary>The sample scripts of FO2tweaksMk2 (shipped in <c>_test_files\samples</c>).</summary>
		Fo2TweaksSamples = 8,

		/// <summary>The optional sample set InventoryFilterMk2 (not shipped).</summary>
		InventoryFilterSamples = 16,

		/// <summary>The optional sample set PartyOrdersMk2 (not shipped).</summary>
		PartyOrdersSamples = 32,
	}
}