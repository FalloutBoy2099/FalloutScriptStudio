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

		/// <summary>The folder with real SSL sources.</summary>
		Samples = 8,
	}
}
