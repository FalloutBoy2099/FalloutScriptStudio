namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class SettingsServiceTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private AppPaths Paths => AppPaths.FromBaseDirectory(this._workspace.Root);

		private SettingsService CreateService()
		{
			return new SettingsService(
				this.Paths,
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
		}

		[Fact]
		public void Defaults_MatchTheLegacyEditor()
		{
			AppSettings settings = new AppSettings();

			Assert.Equal(OptimizationLevel.Basic, settings.Compiler.Optimization);
			Assert.True(settings.Compiler.ShowWarnings);
			Assert.True(settings.Compiler.DebugInfo);
			Assert.True(settings.Compiler.SearchIncludePath);
			Assert.True(settings.Compiler.WarnOnFailedCompile);
			Assert.True(settings.Compiler.MultiThreadedBatch);
			Assert.False(settings.Compiler.ShortCircuit);
			Assert.Equal(PreprocessorKind.Mcpp, settings.Compiler.Preprocessor);
			Assert.True(settings.Compiler.CompileToSourceDirectory);
			Assert.False(settings.Compiler.UseUserCommand);
			Assert.Equal(HighlightTheme.Dark, settings.Editor.Theme);
			Assert.Equal(TextEncodingKind.Ansi, settings.Editor.FileEncoding);
			Assert.True(settings.Editor.TabsToSpaces);
			Assert.Equal(3, settings.Editor.TabSize);
			Assert.True(settings.Editor.EnableParser);
			Assert.True(settings.Editor.ParserWarnings);
			Assert.True(settings.Editor.Autocomplete);
			Assert.True(settings.Editor.AutocompleteColor);
			Assert.True(settings.Editor.AutoInputPairedCharacters);
			Assert.True(settings.Editor.AutoRemoveTrailingSpaces);
			Assert.True(settings.Editor.ShowTips);
			Assert.False(settings.Editor.ShortDescriptions);
			Assert.True(settings.Editor.ShowVerticalRuler);
			Assert.True(settings.Editor.StoreLastPosition);
			Assert.True(settings.Editor.RestoreProjectDocuments);
			Assert.False(settings.Editor.ShowUnusedVariables);
			Assert.Equal(1, settings.Editor.FontIndex);
			Assert.Equal(0, settings.Editor.FontSizeAdjustment);
			Assert.Equal("english", settings.Messages.Language);
			Assert.True(settings.Messages.HighlightComments);
			Assert.Equal(CommentHighlightColor.LightYellow, settings.Messages.CommentColor);
			Assert.True(settings.Flowchart.AutoSaveChart);
			Assert.True(settings.Flowchart.WithoutExitNode);
			Assert.False(settings.Flowchart.AutoUpdate);
			Assert.Equal(-1, settings.Window.SplitterPosition);
			Assert.True(settings.Window.ShowLog);
			Assert.False(settings.IsFirstRunCompleted);
		}

		[Fact]
		public void SaveThenLoad_RoundTripsEverything()
		{
			SettingsService first = this.CreateService();
			first.Settings.Compiler.Optimization = OptimizationLevel.Experimental;
			first.Settings.Compiler.Preprocessor = PreprocessorKind.Watcom;
			first.Settings.Compiler.PreprocessorDefine = "REL";
			first.Settings.Compiler.BackwardCompatibility = true;
			first.Settings.Editor.Theme = HighlightTheme.Light;
			first.Settings.Editor.FileEncoding = TextEncodingKind.Oem866;
			first.Settings.Editor.TabSize = 8;
			first.Settings.Editor.FontSizeAdjustment = -2;
			first.Settings.Editor.RestoreProjectDocuments = false;
			first.Settings.Editor.ShowUnusedVariables = true;
			first.Settings.Messages.CommentColor = CommentHighlightColor.Lavender;
			first.Settings.Messages.Language = "french";
			first.Settings.Paths.OutputDirectory = @"C:\Fallout2\data\scripts";
			first.Settings.Paths.MessageSearchDirectories.Add(@"D:\msg1");
			first.Settings.Paths.MessageSearchDirectories.Add(@"D:\msg2");
			first.Settings.Window.SetPlacement(WindowKind.Main, new WindowPlacement { IsMaximized = true, X = 5, Y = 6, Width = 1000, Height = 700 });
			first.Settings.Window.SetPlacement(WindowKind.Headers, new WindowPlacement { X = 1, Y = 2, Width = 300, Height = 400 });
			first.Recent.AddScript(@"C:\a.ssl");
			first.Recent.AddMessage(@"C:\a.msg");
			first.ScriptPositions.Set("a.ssl", 120);
			first.FoldedProcedures.SetFolded("a.ssl", "start");
			first.SearchHistory.AddQuery("needle");
			first.SearchHistory.Directories.Add(@"C:\src");
			first.PreprocessorDefines.Items.Add("TEST");

			first.Save();

			SettingsService second = this.CreateService();
			second.Load();
			Assert.Equal(OptimizationLevel.Experimental, second.Settings.Compiler.Optimization);
			Assert.Equal(PreprocessorKind.Watcom, second.Settings.Compiler.Preprocessor);
			Assert.Equal("REL", second.Settings.Compiler.PreprocessorDefine);
			Assert.True(second.Settings.Compiler.BackwardCompatibility);
			Assert.Equal(HighlightTheme.Light, second.Settings.Editor.Theme);
			Assert.Equal(TextEncodingKind.Oem866, second.Settings.Editor.FileEncoding);
			Assert.Equal(8, second.Settings.Editor.TabSize);
			Assert.Equal(-2, second.Settings.Editor.FontSizeAdjustment);
			Assert.False(second.Settings.Editor.RestoreProjectDocuments);
			Assert.True(second.Settings.Editor.ShowUnusedVariables);
			Assert.Equal(CommentHighlightColor.Lavender, second.Settings.Messages.CommentColor);
			Assert.Equal("french", second.Settings.Messages.Language);
			Assert.Equal(@"C:\Fallout2\data\scripts", second.Settings.Paths.OutputDirectory);
			Assert.Equal(new[] { @"D:\msg1", @"D:\msg2" }, second.Settings.Paths.MessageSearchDirectories);
			Assert.True(second.Settings.IsFirstRunCompleted);
			Assert.Equal(new WindowPlacement { IsMaximized = true, X = 5, Y = 6, Width = 1000, Height = 700 }, second.Settings.Window.GetPlacement(WindowKind.Main));
			Assert.Equal(300, second.Settings.Window.GetPlacement(WindowKind.Headers)!.Width);
			Assert.Null(second.Settings.Window.GetPlacement(WindowKind.Flowchart));
			Assert.Equal(new[] { @"C:\a.ssl" }, second.Recent.Scripts);
			Assert.Equal(new[] { @"C:\a.msg" }, second.Recent.Messages);
			Assert.Equal(120, second.ScriptPositions.Get("A.SSL"));
			Assert.True(second.FoldedProcedures.IsFolded("a.ssl", "start"));
			Assert.False(second.FoldedProcedures.IsFolded("a.ssl", "other"));
			Assert.Equal(new[] { "needle" }, second.SearchHistory.Queries);
			Assert.Equal(new[] { @"C:\src" }, second.SearchHistory.Directories);
			Assert.Equal(new[] { "TEST" }, second.PreprocessorDefines.Items);
			Assert.False(second.ImportedLegacyData);
		}

		[Fact]
		public void Save_WritesIndentedCamelCaseJsonWithEnumNames()
		{
			SettingsService service = this.CreateService();
			service.Settings.Editor.Theme = HighlightTheme.Light;

			service.Save();

			String json = File.ReadAllText(this.Paths.SettingsFile);
			Assert.Contains("\"editor\": {", json, StringComparison.Ordinal);
			Assert.Contains("\"theme\": \"light\"", json, StringComparison.Ordinal);
			Assert.Contains("\"optimization\": \"basic\"", json, StringComparison.Ordinal);
			Assert.Contains("\n", json, StringComparison.Ordinal);
			Assert.Empty(Directory.GetFiles(this.Paths.SettingsDirectory, "*.tmp"));
		}

		[Fact]
		public async Task SaveAsyncThenLoadAsync_RoundTrips()
		{
			SettingsService first = this.CreateService();
			first.Settings.Editor.TabSize = 6;
			first.Recent.AddScript(@"C:\x.ssl");

			await first.SaveAsync();
			SettingsService second = this.CreateService();
			await second.LoadAsync();

			Assert.Equal(6, second.Settings.Editor.TabSize);
			Assert.Equal(new[] { @"C:\x.ssl" }, second.Recent.Scripts);
		}

		[Fact]
		public void Load_WithNoFilesUsesDefaultsAndCreatesTheFolders()
		{
			SettingsService service = this.CreateService();

			service.Load();

			Assert.Equal(3, service.Settings.Editor.TabSize);
			Assert.False(service.ImportedLegacyData);
			Assert.True(Directory.Exists(this.Paths.SettingsDirectory));
			Assert.True(Directory.Exists(this.Paths.TemplatesDirectory));
			Assert.True(Directory.Exists(this.Paths.ScratchDirectory));
		}

		[Fact]
		public void Load_CorruptFilesFallBackToDefaultsAndAreKept()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.SettingsFile, "{ this is not json");
			File.WriteAllText(this.Paths.RecentFilesFile, "[1,2,3]");
			File.WriteAllText(this.Paths.FoldingFile, String.Empty);
			SettingsService service = this.CreateService();

			service.Load();

			Assert.Equal(3, service.Settings.Editor.TabSize);
			Assert.Empty(service.Recent.Scripts);
			Assert.Empty(service.FoldedProcedures.Scripts);
			Assert.Equal("{ this is not json", File.ReadAllText(this.Paths.SettingsFile + ".corrupt"));

			service.Save();
			Assert.Contains("\"editor\"", File.ReadAllText(this.Paths.SettingsFile), StringComparison.Ordinal);
		}

		[Fact]
		public void Load_UnknownEnumNamesCountAsCorruption_AndOutOfRangeValuesAreRepaired()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.SettingsFile, "{ \"editor\": { \"theme\": \"neon\" } }");
			SettingsService unknownEnum = this.CreateService();
			unknownEnum.Load();
			Assert.Equal(HighlightTheme.Dark, unknownEnum.Settings.Editor.Theme);

			File.WriteAllText(this.Paths.SettingsFile, "{ \"editor\": { \"tabSize\": 99 }, \"messages\": { \"language\": \"\" }, \"paths\": null }");
			SettingsService outOfRange = this.CreateService();
			outOfRange.Load();
			Assert.Equal(3, outOfRange.Settings.Editor.TabSize);
			Assert.Equal("english", outOfRange.Settings.Messages.Language);
			Assert.NotNull(outOfRange.Settings.Paths);
		}

		[Fact]
		public void Load_IgnoresUnknownPropertiesAndPartialFiles()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.SettingsFile, "{ \"future\": 1, \"compiler\": { \"optimization\": \"full\" } }");
			SettingsService service = this.CreateService();

			service.Load();

			Assert.Equal(OptimizationLevel.Full, service.Settings.Compiler.Optimization);
			Assert.True(service.Settings.Compiler.ShowWarnings);
		}

		[Fact]
		public void Load_RaisesSettingsChanged_AndApplyReplacesTheSettings()
		{
			SettingsService service = this.CreateService();
			List<AppSettings> seen = new List<AppSettings>();
			service.SettingsChanged += (_, args) => seen.Add(args.Settings);

			service.Load();
			AppSettings edited = service.Settings.Clone();
			edited.Editor.TabSize = 5;
			service.Apply(edited);
			service.Update(settings => settings.Editor.TabSize = 7);

			Assert.Equal(3, seen.Count);
			Assert.Same(edited, service.Settings);
			Assert.Equal(7, service.Settings.Editor.TabSize);
			Assert.Equal(3, new AppSettings().Editor.TabSize);
		}

		[Fact]
		public void Clone_IsADeepCopy()
		{
			AppSettings original = new AppSettings();
			original.Paths.MessageSearchDirectories.Add("a");
			original.Window.SetPlacement(WindowKind.Main, new WindowPlacement { Width = 10 });

			AppSettings copy = original.Clone();
			copy.Paths.MessageSearchDirectories.Add("b");
			copy.Editor.TabSize = 9;

			Assert.Single(original.Paths.MessageSearchDirectories);
			Assert.Equal(3, original.Editor.TabSize);
			Assert.Equal(10, copy.Window.GetPlacement(WindowKind.Main)!.Width);
		}

		[Fact]
		public void Save_FailureIsReportedAfterTryingEveryFile()
		{
			SettingsService service = this.CreateService();
			service.Load();
			Directory.CreateDirectory(this.Paths.SettingsFile); // a directory where a file is expected

			Exception failure = Assert.ThrowsAny<Exception>(() => service.Save());

			Assert.True(failure is IOException or UnauthorizedAccessException);
			Assert.True(File.Exists(this.Paths.RecentFilesFile));
		}

		[Fact]
		public void Load_ImportsTheLegacyFilesWhenNoNewSettingsExist()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			using (BinaryWriter writer = new BinaryWriter(File.Create(this.Paths.LegacyRecentFile), Encoding.UTF8))
			{
				writer.Write((Byte)1);
				writer.Write((Byte)0);
				writer.Write(@"C:\old.ssl");
				writer.Write((Byte)1);
				writer.Write("old.ssl");
				writer.Write((UInt16)77);
			}

			File.WriteAllLines(this.Paths.LegacySearchHistoryFile, new[] { "alpha", "beta" });
			SettingsService service = this.CreateService();

			service.Load();

			Assert.True(service.ImportedLegacyData);
			Assert.Equal(new[] { @"C:\old.ssl" }, service.Recent.Scripts);
			Assert.Equal(77, service.ScriptPositions.Get("old.ssl"));
			Assert.Equal(new[] { "alpha", "beta" }, service.SearchHistory.Queries);
			Assert.Equal(3, service.Settings.Editor.TabSize);

			service.Save();
			Assert.True(File.Exists(this.Paths.LegacyRecentFile));
			SettingsService reloaded = this.CreateService();
			reloaded.Load();
			Assert.False(reloaded.ImportedLegacyData);
			Assert.Equal(new[] { @"C:\old.ssl" }, reloaded.Recent.Scripts);
		}

		[Fact]
		public void AddRecentScript_ForgetsThePositionOfTheDroppedScript()
		{
			SettingsService service = this.CreateService();
			service.AddRecentScript(@"C:\first.ssl");
			service.ScriptPositions.Set("first.ssl", 50);
			for (Int32 i = 0; i < RecentFiles.MaxItems; i++)
			{
				service.AddRecentScript($@"C:\script{i}.ssl");
			}

			Assert.Equal(RecentFiles.MaxItems, service.Recent.Scripts.Count);
			Assert.DoesNotContain(@"C:\first.ssl", service.Recent.Scripts);
			Assert.Equal(0, service.ScriptPositions.Get("first.ssl"));
		}
	}
}
