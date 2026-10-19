namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using FalloutScriptStudio.TestSupport;

	/// <summary>
	/// Locates the real sfall compiler tools (<c>compile.exe</c>, <c>parser.dll</c>, <c>int2ssl.exe</c>), the real sample scripts and the
	/// parser host that the integration tests run against. Everything is looked for in the folder <c>_test_files</c> of the repository
	/// (see <see cref="TestFiles"/>); the tests are skipped (see <see cref="RealToolFactAttribute"/>) when something is missing.
	/// </summary>
	internal static class SslcTools
	{
		/// <summary>The environment variable that overrides the folder of the tools.</summary>
		public const String DirectoryVariable = TestFiles.ToolsVariable;

		/// <summary>The environment variable that overrides the folder that contains the sample sets with real SSL sources.</summary>
		public const String SamplesVariable = TestFiles.SamplesVariable;

		/// <summary>Gets the folder that holds the tools (<c>_test_files\tools</c> by default).</summary>
		public static String Directory => TestFiles.Tools;

		/// <summary>Gets the folder that contains the sample sets (<c>_test_files\samples</c> by default).</summary>
		public static String SamplesDirectory => TestFiles.Samples;

		/// <summary>Gets the sources of FO2tweaksMk2 (self-contained scripts, shipped in the repository).</summary>
		public static String Fo2TweaksSources => Path.Combine(SamplesDirectory, "FO2tweaksMk2", "source");

		/// <summary>Gets the sources of InventoryFilterMk2 (one script that includes many modules and a header); an optional sample set.</summary>
		public static String InventoryFilterSources => Path.Combine(SamplesDirectory, "InventoryFilterMk2", "source");

		/// <summary>Gets the sources of PartyOrdersMk2 (needs headers that are not in the repository); an optional sample set.</summary>
		public static String PartyOrdersSources => Path.Combine(SamplesDirectory, "PartyOrdersMk2", "source");

		/// <summary>Gets the path of <c>parser.dll</c>.</summary>
		public static String ParserDll => Path.Combine(Directory, "parser.dll");

		/// <summary>Gets the path of <c>compile.exe</c>.</summary>
		public static String CompileExe => Path.Combine(Directory, "compile.exe");

		/// <summary>Gets the path of <c>int2ssl.exe</c>.</summary>
		public static String Int2SslExe => Path.Combine(Directory, "int2ssl.exe");

		/// <summary>Gets the path of the parser host that was built next to the tests.</summary>
		public static String HostExe => Path.Combine(AppContext.BaseDirectory, "ParserHost", "FalloutScriptStudio.ParserHost.exe");

		/// <summary>Gets a value indicating whether <c>parser.dll</c> and the host are available (and the OS is Windows).</summary>
		public static Boolean HasParser => OperatingSystem.IsWindows() && File.Exists(ParserDll) && File.Exists(HostExe);

		/// <summary>Gets a value indicating whether <c>compile.exe</c> is available.</summary>
		public static Boolean HasCompiler => OperatingSystem.IsWindows() && File.Exists(CompileExe);

		/// <summary>Gets a value indicating whether <c>int2ssl.exe</c> is available.</summary>
		public static Boolean HasDecompiler => OperatingSystem.IsWindows() && File.Exists(Int2SslExe);

		/// <summary>Gets a value indicating whether the sample scripts of FO2tweaksMk2 exist.</summary>
		public static Boolean HasFo2TweaksSamples => System.IO.Directory.Exists(Fo2TweaksSources);

		/// <summary>Gets a value indicating whether the optional sample set InventoryFilterMk2 exists.</summary>
		public static Boolean HasInventoryFilterSamples => System.IO.Directory.Exists(InventoryFilterSources);

		/// <summary>Gets a value indicating whether the optional sample set PartyOrdersMk2 exists.</summary>
		public static Boolean HasPartyOrdersSamples => System.IO.Directory.Exists(PartyOrdersSources);

		/// <summary>Gets why a set of tools is not usable, or <c>null</c> when they are all available.</summary>
		public static String? GetSkipReason(RealTools tools)
		{
			String hint = " They are expected in " + TestFiles.FolderName + "\\tools (or in the folder of the environment variable " + DirectoryVariable + ").";
			if (tools.HasFlag(RealTools.Parser) && !HasParser)
			{
				return "parser.dll (" + ParserDll + ") or the built parser host (" + HostExe + ") is not available." + hint;
			}

			if (tools.HasFlag(RealTools.Compiler) && !HasCompiler)
			{
				return "compile.exe (" + CompileExe + ") is not available." + hint;
			}

			if (tools.HasFlag(RealTools.Decompiler) && !HasDecompiler)
			{
				return "int2ssl.exe (" + Int2SslExe + ") is not available." + hint;
			}

			if (tools.HasFlag(RealTools.Fo2TweaksSamples) && !HasFo2TweaksSamples)
			{
				return "The sample scripts of FO2tweaksMk2 (" + Fo2TweaksSources + ") are not available. Set " + SamplesVariable + " to override.";
			}

			if (tools.HasFlag(RealTools.InventoryFilterSamples) && !HasInventoryFilterSamples)
			{
				return "The optional sample set InventoryFilterMk2 (" + InventoryFilterSources + ") is not available.";
			}

			if (tools.HasFlag(RealTools.PartyOrdersSamples) && !HasPartyOrdersSamples)
			{
				return "The optional sample set PartyOrdersMk2 (" + PartyOrdersSources + ") is not available.";
			}

			return null;
		}
	}
}