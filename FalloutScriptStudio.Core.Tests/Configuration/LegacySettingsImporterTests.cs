namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public class LegacySettingsImporterTests
	{
		private readonly LegacySettingsImporter _importer = new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance);

		/// <summary>Writes a settings.dat exactly like the legacy <c>Settings.Save</c> did, with values that differ from the defaults.</summary>
		private static Byte[] CreateLegacySettings(Boolean truncate = false, Boolean useWatcom = false, Boolean useMcpp = false)
		{
			using MemoryStream stream = new MemoryStream();
			using (BinaryWriter bw = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				bw.Write((Byte)255);                // firstRun
				bw.Write(true);                     // allowDefine
				bw.Write((Byte)0);                  // unused
				bw.Write((Byte)0);                  // highlight: light
				bw.Write((Byte)1);                  // encoding: cp866
				bw.Write((Byte)2);                  // optimize: full
				bw.Write(false);                    // showWarnings
				bw.Write(false);                    // showDebug
				bw.Write(false);                    // searchIncludePath
				bw.Write(@"C:\Fallout2\data\scripts"); // outputDir
				bw.Write(false);                    // warnOnFailedCompile
				bw.Write(false);                    // multiThreaded
				bw.Write(@"D:\mass");               // lastMassCompile
				bw.Write(@"D:\search");             // lastSearchPath
				bw.Write(true);                     // window maximized
				bw.Write(11);                       // x
				bw.Write(22);                       // y
				bw.Write(1280);                     // width
				bw.Write(720);                      // height
				bw.Write(123);                      // editorSplitterPosition
				bw.Write(true);                     // autoOpenMsgs
				bw.Write(456);                      // editorSplitterPosition2
				bw.Write(@"D:\headers");            // pathHeadersFiles
				bw.Write("russian");                // language
				bw.Write(false);                    // tabsToSpaces
				bw.Write(4);                        // tabSize
				bw.Write(false);                    // enableParser
				bw.Write(true);                     // shortCircuit
				bw.Write(false);                    // autocomplete
				bw.Write(false);                    // showLog
				bw.Write(false);                    // parserWarn
				bw.Write(useWatcom);                // useWatcom
				bw.Write("BUILD_X");                // preprocDef
				bw.Write(false);                    // ignoreCompPath
				bw.Write((Byte)2);                  // msgListPath count
				bw.Write(@"E:\msg1");
				bw.Write(@"E:\msg2");
				bw.Write(true);                     // openMsgEditor
				bw.Write(true);                     // userCmdCompile
				bw.Write(false);                    // msgHighlightComment
				bw.Write((Byte)2);                  // msgHighlightColor: lavender
				bw.Write((Byte)3);                  // msgFontSize
				bw.Write(@"D:\headers\scripts.h");  // pathScriptsHFile
				bw.Write(true);                     // associateID
				bw.Write(true);                     // autoUpdate
				bw.Write(false);                    // autoSaveChart
				bw.Write(true);                     // autoArrange
				bw.Write(false);                    // woExitNode
				bw.Write(useMcpp);                  // useMcpp
				bw.Write(false);                    // autocompleteColor
				bw.Write(false);                    // autoInputPaired
				bw.Write(true);                     // showTabsChar
				bw.Write(false);                    // autoTrailingSpaces
				bw.Write(false);                    // showTips
				bw.Write(true);                     // shortDesc
				bw.Write(true);                     // autoHideNodes
				bw.Write((Byte)3);                  // selectFont
				bw.Write((SByte)(-2));              // sizeFont
				bw.Write(false);                    // showVRuler
				bw.Write(false);                    // storeLastPosition
				bw.Write(true);                     // saveScriptUTF8
				bw.Write(true);                     // decompileF1
				bw.Write(false);                    // winAPITextRender
				bw.Write(@"C:\Tools\npp.exe");      // external editor
				bw.Write(true);                     // oldDecompile
				bw.Write(true);                     // searchIgnoreCase
				bw.Write(true);                     // searchWholeWord
				bw.Write(@"D:\project");            // solutionProjectFolder
			}

			Byte[] bytes = stream.ToArray();
			return truncate ? bytes[..(bytes.Length / 2)] : bytes;
		}

		[Fact]
		public void ReadSettings_MapsEveryFieldInTheLegacyOrder()
		{
			List<String> problems = new List<String>();

			AppSettings settings = this._importer.ReadSettings(new MemoryStream(CreateLegacySettings()), problems);

			Assert.Empty(problems);
			Assert.True(settings.IsFirstRunCompleted);
			Assert.True(settings.Messages.RegisterScriptAllowDefine);
			Assert.Equal(HighlightTheme.Light, settings.Editor.Theme);
			Assert.Equal(TextEncodingKind.Oem866, settings.Editor.FileEncoding);
			Assert.Equal(OptimizationLevel.Full, settings.Compiler.Optimization);
			Assert.False(settings.Compiler.ShowWarnings);
			Assert.False(settings.Compiler.DebugInfo);
			Assert.False(settings.Compiler.SearchIncludePath);
			Assert.Equal(@"C:\Fallout2\data\scripts", settings.Paths.OutputDirectory);
			Assert.False(settings.Compiler.WarnOnFailedCompile);
			Assert.False(settings.Compiler.MultiThreadedBatch);
			Assert.Equal(@"D:\mass", settings.Paths.LastMassCompileDirectory);
			Assert.Equal(@"D:\search", settings.Paths.LastSearchDirectory);
			Assert.Equal(new WindowPlacement { IsMaximized = true, X = 11, Y = 22, Width = 1280, Height = 720 }, settings.Window.GetPlacement(WindowKind.Main));
			Assert.Equal(123, settings.Window.SplitterPosition);
			Assert.True(settings.Messages.AutoOpenMessageFiles);
			Assert.Equal(456, settings.Window.SecondarySplitterPosition);
			Assert.Equal(@"D:\headers", settings.Paths.HeadersDirectory);
			Assert.Equal("russian", settings.Messages.Language);
			Assert.False(settings.Editor.TabsToSpaces);
			Assert.Equal(4, settings.Editor.TabSize);
			Assert.False(settings.Editor.EnableParser);
			Assert.True(settings.Compiler.ShortCircuit);
			Assert.False(settings.Editor.Autocomplete);
			Assert.False(settings.Window.ShowLog);
			Assert.False(settings.Editor.ParserWarnings);
			Assert.Equal("BUILD_X", settings.Compiler.PreprocessorDefine);
			Assert.False(settings.Compiler.CompileToSourceDirectory);
			Assert.Equal(new[] { @"E:\msg1", @"E:\msg2" }, settings.Paths.MessageSearchDirectories);
			Assert.True(settings.Messages.OpenEditorAutomatically);
			Assert.True(settings.Compiler.UseUserCommand);
			Assert.False(settings.Messages.HighlightComments);
			Assert.Equal(CommentHighlightColor.Lavender, settings.Messages.CommentColor);
			Assert.Equal(3, settings.Messages.FontSizeIndex);
			Assert.Equal(@"D:\headers\scripts.h", settings.Paths.ScriptsHeaderFile);
			Assert.True(settings.Messages.AssociateByScriptId);
			Assert.True(settings.Flowchart.AutoUpdate);
			Assert.False(settings.Flowchart.AutoSaveChart);
			Assert.True(settings.Flowchart.AutoArrange);
			Assert.False(settings.Flowchart.WithoutExitNode);
			Assert.Equal(PreprocessorKind.Builtin, settings.Compiler.Preprocessor);
			Assert.False(settings.Editor.AutocompleteColor);
			Assert.False(settings.Editor.AutoInputPairedCharacters);
			Assert.True(settings.Editor.ShowTabCharacters);
			Assert.False(settings.Editor.AutoRemoveTrailingSpaces);
			Assert.False(settings.Editor.ShowTips);
			Assert.True(settings.Editor.ShortDescriptions);
			Assert.True(settings.Flowchart.AutoHideNodes);
			Assert.Equal(3, settings.Editor.FontIndex);
			Assert.Equal(-2, settings.Editor.FontSizeAdjustment);
			Assert.False(settings.Editor.ShowVerticalRuler);
			Assert.False(settings.Editor.StoreLastPosition);
			Assert.True(settings.Editor.SaveScriptsAsUtf8);
			Assert.True(settings.Compiler.DecompileForFallout1);
			Assert.Equal(@"C:\Tools\npp.exe", settings.Paths.ExternalEditorPath);
			Assert.True(settings.Compiler.UseLegacyDecompiler);
			Assert.Equal(DecompilerKind.ExternalInt2Ssl, settings.Compiler.Decompiler);
			Assert.True(settings.Editor.SearchIgnoreCase);
			Assert.True(settings.Editor.SearchWholeWord);
			Assert.Equal(@"D:\project", settings.Paths.SolutionFolder);
		}

		[Fact]
		public void ReadSettings_TruncatedFileKeepsTheValuesReadSoFar()
		{
			List<String> problems = new List<String>();

			AppSettings settings = this._importer.ReadSettings(new MemoryStream(CreateLegacySettings(true)), problems);

			Assert.Single(problems);
			Assert.Equal(OptimizationLevel.Full, settings.Compiler.Optimization);
			Assert.Equal(@"C:\Fallout2\data\scripts", settings.Paths.OutputDirectory);
			Assert.Equal(new AppSettings().Editor.SearchWholeWord, settings.Editor.SearchWholeWord);
		}

		[Fact]
		public void ReadSettings_EmptyOrGarbageStreamsNeverThrow()
		{
			List<String> problems = new List<String>();

			AppSettings empty = this._importer.ReadSettings(new MemoryStream(), problems);
			AppSettings garbage = this._importer.ReadSettings(new MemoryStream(new Byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }), problems);

			Assert.Equal(2, problems.Count);
			Assert.Equal(3, empty.Editor.TabSize);
			Assert.Equal(3, garbage.Editor.TabSize);
		}

		[Fact]
		public void ReadSettings_OldOptimizationValueAndUnknownEnumsAreTolerated()
		{
			Byte[] bytes = CreateLegacySettings();
			bytes[5] = 255; // optimize
			bytes[3] = 9;   // highlight

			AppSettings settings = this._importer.ReadSettings(new MemoryStream(bytes), new List<String>());

			Assert.Equal(OptimizationLevel.Basic, settings.Compiler.Optimization);
			Assert.Equal(HighlightTheme.Dark, settings.Editor.Theme);
		}

		[Fact]
		public void ReadSettings_MapsThePreprocessorFlags()
		{
			PreprocessorKind Read(Boolean watcom, Boolean mcpp)
			{
				return this._importer.ReadSettings(new MemoryStream(CreateLegacySettings(false, watcom, mcpp)), new List<String>()).Compiler.Preprocessor;
			}

			Assert.Equal(PreprocessorKind.Builtin, Read(false, false));
			Assert.Equal(PreprocessorKind.Mcpp, Read(false, true));
			Assert.Equal(PreprocessorKind.Watcom, Read(true, false));
			Assert.Equal(PreprocessorKind.Watcom, Read(true, true));
		}

		[Fact]
		public void ReadRecent_ReadsScriptsMessagesAndPositions()
		{
			using MemoryStream stream = new MemoryStream();
			using (BinaryWriter bw = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				bw.Write((Byte)2);
				bw.Write((Byte)1);
				bw.Write(@"C:\a.ssl");
				bw.Write(@"C:\b.ssl");
				bw.Write(@"C:\a.msg");
				bw.Write((Byte)2);
				bw.Write("a.ssl");
				bw.Write((UInt16)40);
				bw.Write("b.ssl");
				bw.Write((UInt16)65535);
			}

			stream.Position = 0;
			List<String> problems = new List<String>();

			(RecentFiles recent, ScriptPositions positions) = this._importer.ReadRecent(stream, problems);

			Assert.Empty(problems);
			Assert.Equal(new[] { @"C:\a.ssl", @"C:\b.ssl" }, recent.Scripts);
			Assert.Equal(new[] { @"C:\a.msg" }, recent.Messages);
			Assert.Equal(40, positions.Get("a.ssl"));
			Assert.Equal(65535, positions.Get("b.ssl"));
		}

		[Fact]
		public void ReadRecent_TruncatedStreamKeepsWhatWasRead()
		{
			using MemoryStream stream = new MemoryStream();
			using (BinaryWriter bw = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				bw.Write((Byte)3);
				bw.Write((Byte)0);
				bw.Write(@"C:\a.ssl");
			}

			stream.Position = 0;
			List<String> problems = new List<String>();

			(RecentFiles recent, _) = this._importer.ReadRecent(stream, problems);

			Assert.Single(problems);
			Assert.Equal(new[] { @"C:\a.ssl" }, recent.Scripts);
		}

		[Fact]
		public void ReadFolding_ReadsProceduresPerScriptAndSkipsEmptyScripts()
		{
			using MemoryStream stream = new MemoryStream();
			using (BinaryWriter bw = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				bw.Write(3);
				bw.Write("a.ssl");
				bw.Write(2);
				bw.Write("start");
				bw.Write("talk_p_proc");
				bw.Write("empty.ssl");
				bw.Write(0);
				bw.Write("b.ssl");
				bw.Write(1);
				bw.Write("map_enter_p_proc");
			}

			stream.Position = 0;
			List<String> problems = new List<String>();

			FoldedProcedures folding = this._importer.ReadFolding(stream, problems);

			Assert.Empty(problems);
			Assert.True(folding.IsFolded("a.ssl", "start"));
			Assert.True(folding.IsFolded("a.ssl", "talk_p_proc"));
			Assert.True(folding.IsFolded("b.ssl", "map_enter_p_proc"));
			Assert.False(folding.IsFolded("empty.ssl", "anything"));
			Assert.Equal(2, folding.Scripts.Count);
		}

		[Fact]
		public void ReadFolding_EmptyFileMeansNothingIsFolded()
		{
			List<String> problems = new List<String>();

			FoldedProcedures folding = this._importer.ReadFolding(new MemoryStream(), problems);

			Assert.Empty(problems);
			Assert.Empty(folding.Scripts);
		}
	}
}
