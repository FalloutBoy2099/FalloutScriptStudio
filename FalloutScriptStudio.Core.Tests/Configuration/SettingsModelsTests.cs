namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Xunit;

	public class SettingsModelsTests
	{
		[Fact]
		public void RecentFiles_MovesRepeatedEntriesToTheEndIgnoringCase()
		{
			RecentFiles recent = new RecentFiles();
			recent.AddScript(@"C:\a.ssl");
			recent.AddScript(@"C:\b.ssl");

			String? dropped = recent.AddScript(@"C:\A.SSL");

			Assert.Null(dropped);
			Assert.Equal(new[] { @"C:\b.ssl", @"C:\A.SSL" }, recent.Scripts);
		}

		[Fact]
		public void RecentFiles_DropsTheOldestEntryWhenFull()
		{
			RecentFiles recent = new RecentFiles();
			for (Int32 i = 0; i < RecentFiles.MaxItems; i++)
			{
				recent.AddMessage($"m{i}.msg");
			}

			String? dropped = recent.AddMessage("new.msg");

			Assert.Equal("m0.msg", dropped);
			Assert.Equal(RecentFiles.MaxItems, recent.Messages.Count);
			Assert.Equal("new.msg", recent.Messages[^1]);
			recent.RemoveMessage("NEW.MSG");
			Assert.DoesNotContain("new.msg", recent.Messages);
			recent.AddScript("x.ssl");
			recent.ClearScripts();
			Assert.Empty(recent.Scripts);
		}

		[Fact]
		public void ScriptPositions_IgnoreLinesNearTheTop()
		{
			ScriptPositions positions = new ScriptPositions();

			positions.Set("a.ssl", 9);
			positions.Set("b.ssl", 10);
			positions.Set("c.ssl", 1_000_000);

			Assert.Equal(0, positions.Get("a.ssl"));
			Assert.Equal(10, positions.Get("b.ssl"));
			Assert.Equal(65535, positions.Get("c.ssl"));
			positions.Remove("b.ssl");
			Assert.Equal(0, positions.Get("b.ssl"));
		}

		[Fact]
		public void FoldedProcedures_ForgetScriptsWithoutFoldedProcedures()
		{
			FoldedProcedures folding = new FoldedProcedures();
			folding.SetFolded("a.ssl", "start");
			folding.SetFolded("a.ssl", "talk");

			folding.SetUnfolded("a.ssl", "start");
			Assert.False(folding.IsFolded("a.ssl", "start"));
			Assert.True(folding.IsFolded("a.ssl", "talk"));

			folding.SetUnfolded("a.ssl", "talk");
			folding.SetUnfolded("missing.ssl", "x");
			Assert.Empty(folding.Scripts);
		}

		[Fact]
		public void SearchHistory_KeepsMostRecentFirstWithoutDuplicatesAndWithinTheLimit()
		{
			SearchHistory history = new SearchHistory();
			history.AddQuery("one");
			history.AddQuery("two");
			history.AddQuery("one");
			history.AddQuery(String.Empty);

			Assert.Equal(new[] { "one", "two" }, history.Queries);

			for (Int32 i = 0; i < SearchHistory.MaxQueries + 20; i++)
			{
				history.AddQuery("q" + i);
			}

			Assert.Equal(SearchHistory.MaxQueries, history.Queries.Count);
			Assert.Equal("q169", history.Queries[0]);
		}

		[Fact]
		public void AppPaths_AreDerivedFromTheBaseDirectory()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(@"C:\Editor");

			Assert.Equal(@"C:\Editor", paths.ProgramDirectory);
			Assert.Equal(@"C:\Editor\settings", paths.SettingsDirectory);
			Assert.Equal(@"C:\Editor\resources", paths.ResourcesDirectory);
			Assert.Equal(@"C:\Editor\descriptions", paths.DescriptionsDirectory);
			Assert.Equal(@"C:\Editor\scrTemp", paths.ScratchDirectory);
			Assert.Equal(@"C:\Editor\resources\templates", paths.TemplatesDirectory);
			Assert.Equal(@"C:\Editor\resources\fonts", paths.FontsDirectory);
			Assert.Equal(@"C:\Editor\resources\compile.exe", paths.GetResourcePath("compile.exe"));
			Assert.Equal(@"C:\Editor\settings\settings.dat", paths.LegacySettingsFile);
		}

		[Fact]
		public void AppPaths_CreateAndCleanTheScratchFolder()
		{
			using TempWorkspace workspace = new TempWorkspace();
			AppPaths paths = AppPaths.FromBaseDirectory(Path.Combine(workspace.Root, "install"));

			paths.EnsureCreated();
			Assert.True(Directory.Exists(paths.SettingsDirectory));
			Assert.True(Directory.Exists(paths.TemplatesDirectory));

			File.WriteAllText(Path.Combine(paths.ScratchDirectory, "left-over.ssl"), "x");
			paths.PrepareScratchDirectory();
			Assert.Empty(Directory.GetFiles(paths.ScratchDirectory));

			paths.DeleteScratchDirectory();
			Assert.False(Directory.Exists(paths.ScratchDirectory));
			paths.PrepareScratchDirectory();
			Assert.True(Directory.Exists(paths.ScratchDirectory));
		}

		[Fact]
		public void Projection_BuildsCompilerOptionsFromSettingsAndPaths()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(@"C:\Editor");
			AppSettings settings = new AppSettings();
			settings.Compiler.Optimization = OptimizationLevel.Full;
			settings.Compiler.Preprocessor = PreprocessorKind.Watcom;
			settings.Compiler.PreprocessorDefine = "REL";
			settings.Compiler.ShortCircuit = true;
			settings.Compiler.CompileToSourceDirectory = false;
			settings.Compiler.Decompiler = DecompilerKind.ExternalInt2Ssl;
			settings.Compiler.ExternalDecompilerPath = @"D:\tools\int2ssl.exe";
			settings.Compiler.DecompileForFallout1 = true;
			settings.Compiler.UseLegacyDecompiler = true;
			settings.Paths.OutputDirectory = @"D:\out";
			settings.Paths.HeadersDirectory = @"D:\h";
			settings.Paths.ScriptsHeaderFile = @"D:\h\scripts.h";
			settings.Editor.TabSize = 5;
			settings.Editor.TabsToSpaces = false;

			CompilerOptions options = settings.ToCompilerOptions(paths);

			Assert.Equal(@"C:\Editor", options.ProgramDirectory);
			Assert.Equal(@"C:\Editor\resources", options.ResourcesDirectory);
			Assert.Equal(@"C:\Editor\scrTemp", options.ScratchDirectory);
			Assert.Equal(OptimizationLevel.Full, options.Optimization);
			Assert.Equal(PreprocessorKind.Watcom, options.Preprocessor);
			Assert.Equal("REL", options.PreprocessorDefine);
			Assert.True(options.ShortCircuit);
			Assert.False(options.CompileToSourceDirectory);
			Assert.Equal(@"D:\out", options.OutputDirectory);
			Assert.Equal(DecompilerKind.ExternalInt2Ssl, options.Decompiler);
			Assert.Equal(@"D:\tools\int2ssl.exe", options.ExternalDecompilerPath);
			Assert.True(options.DecompileForFallout1);
			Assert.True(options.UseLegacyDecompiler);
			Assert.True(options.SearchesIncludes);
			Assert.Equal(5, options.TabSize);
			Assert.False(options.TabsToSpaces);
		}

		[Fact]
		public void Projection_BuildsMessageAndBatchOptions()
		{
			AppSettings settings = new AppSettings();
			settings.Paths.OutputDirectory = @"D:\out";
			settings.Paths.MessageSearchDirectories.AddRange(new[] { @"D:\m1", @"D:\m2" });
			settings.Messages.Language = "german";
			settings.Messages.AssociateByScriptId = true;
			settings.Editor.FileEncoding = TextEncodingKind.Oem866;
			settings.Compiler.MultiThreadedBatch = false;

			MessageFileOptions messageOptions = settings.ToMessageFileOptions();
			BatchCompileOptions batchOptions = settings.ToBatchCompileOptions();

			Assert.Equal(@"D:\out", messageOptions.OutputDirectory);
			Assert.Equal("german", messageOptions.Language);
			Assert.True(messageOptions.AssociateByScriptId);
			Assert.Equal(new[] { @"D:\m1", @"D:\m2" }, messageOptions.SearchDirectories.ToArray());
			Assert.Equal(866, messageOptions.Encoding.CodePage);
			Assert.False(batchOptions.MultiThreaded);
		}

		[Fact]
		public void TextEncodingKind_MapsToTheNamedEncodings()
		{
			Assert.Same(TextEncodings.Oem866, TextEncodingKind.Oem866.ToEncoding());
			Assert.Same(TextEncodings.Ansi, TextEncodingKind.Ansi.ToEncoding());
		}

		[Fact]
		public void KnownFonts_AdjustTheSizeOfEveryBundledFont()
		{
			Assert.Equal(10.5, KnownFonts.GetBaseSize("Consolas"));
			Assert.Equal(11.75, KnownFonts.GetBaseSize("Ubuntu Mono"));
			Assert.Equal(KnownFonts.DefaultSize, KnownFonts.GetBaseSize("Unknown"));
		}
	}
}