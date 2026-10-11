namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;

	/// <summary>
	/// Locates the real sfall compiler tools (<c>compile.exe</c>, <c>parser.dll</c>, <c>int2ssl.exe</c>) and the parser host that the
	/// integration tests run against. The tests are skipped (see <see cref="RealToolFactAttribute"/>) when something is missing.
	/// </summary>
	internal static class SslcTools
	{
		/// <summary>The environment variable that overrides the folder of the tools.</summary>
		public const String DirectoryVariable = "FSS_SSLC_DIR";

		/// <summary>The environment variable that overrides the folder that contains the repositories with real SSL sources (smoke tests).</summary>
		public const String SamplesVariable = "FSS_SSL_SAMPLES";

		private const String DefaultDirectory = @"D:\Git\Fallout\FO2tweaksMk2\tools\sslc-sfall5";
		private const String DefaultSamples = @"D:\Git\Fallout";

		/// <summary>Gets the folder that holds the tools.</summary>
		public static String Directory => Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } custom ? custom : DefaultDirectory;

		/// <summary>Gets the folder that contains the repositories FO2tweaksMk2, InventoryFilterMk2 and PartyOrdersMk2.</summary>
		public static String SamplesDirectory => Environment.GetEnvironmentVariable(SamplesVariable) is { Length: > 0 } custom ? custom : DefaultSamples;

		/// <summary>Gets the sources of FO2tweaksMk2 (self-contained scripts).</summary>
		public static String Fo2TweaksSources => Path.Combine(SamplesDirectory, "FO2tweaksMk2", "source");

		/// <summary>Gets the sources of InventoryFilterMk2 (one script that includes many modules and a header).</summary>
		public static String InventoryFilterSources => Path.Combine(SamplesDirectory, "InventoryFilterMk2", "source");

		/// <summary>Gets the sources of PartyOrdersMk2 (needs headers that are not in the repository).</summary>
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

		/// <summary>Gets a value indicating whether the repositories with real SSL sources exist.</summary>
		public static Boolean HasSamples => System.IO.Directory.Exists(Fo2TweaksSources) && System.IO.Directory.Exists(InventoryFilterSources) && System.IO.Directory.Exists(PartyOrdersSources);

		/// <summary>Gets why a set of tools is not usable, or <c>null</c> when they are all available.</summary>
		public static String? GetSkipReason(RealTools tools)
		{
			String hint = " Set " + DirectoryVariable + " to the folder of the sfall compiler (compile.exe, parser.dll, int2ssl.exe).";
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

			if (tools.HasFlag(RealTools.Samples) && !HasSamples)
			{
				return "The real SSL sources (" + SamplesDirectory + ") are not available. Set " + SamplesVariable + " to override.";
			}

			return null;
		}
	}
}
