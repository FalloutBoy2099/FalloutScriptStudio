namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The project file <c>fss-project.json</c>: relative paths, unknown properties and the safety rules shared with the global settings.</summary>
	public sealed class ProjectSettingsFileTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("project-file");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		private String Project => this._folder.Path;

		private String FilePath => Path.Combine(this.Project, ProjectSettings.FileName);

		[Fact]
		public void TryParse_ReportsWhereTheContentIsNotValid()
		{
			Byte[] content = System.Text.Encoding.UTF8.GetBytes("{\r\n  \"name\": \"Mod\",\r\n  \"optimization\" \"full\"\r\n}");

			Boolean valid = ProjectSettingsFile.TryParse(content, this.Project, out ProjectSettings? settings, out ProjectFileError? error);

			Assert.False(valid);
			Assert.Null(settings);
			Assert.NotNull(error);
			Assert.Equal(3, error.Line);
			Assert.True(error.Column > 0);
			Assert.False(String.IsNullOrWhiteSpace(error.Message));
			Assert.DoesNotContain("LineNumber", error.Message, StringComparison.Ordinal);
			Assert.DoesNotContain("BytePositionInLine", error.Message, StringComparison.Ordinal);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("null")]
		[InlineData("{ \"optimization\": \"warp\" }")]
		[InlineData("{ \"name\": ")]
		public void TryParse_RefusesEmptyTruncatedAndUnknownContent(String json)
		{
			Assert.False(ProjectSettingsFile.TryParse(System.Text.Encoding.UTF8.GetBytes(json), this.Project, out ProjectSettings? settings, out ProjectFileError? error));
			Assert.Null(settings);
			Assert.NotNull(error);
		}

		[Fact]
		public void TryParse_AcceptsTheByteOrderMarkOfWindowsEditors_AndUtf16Files()
		{
			String json = "{ \"name\": \"Café Mod\", \"optimization\": \"full\" }";
			Byte[] bom = new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes(json)).ToArray();
			Byte[] utf16 = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(json)).ToArray();
			Byte[] utf16Big = System.Text.Encoding.BigEndianUnicode.GetPreamble().Concat(System.Text.Encoding.BigEndianUnicode.GetBytes(json)).ToArray();

			foreach (Byte[] content in new[] { bom, utf16, utf16Big })
			{
				Assert.True(ProjectSettingsFile.TryParse(content, this.Project, out ProjectSettings? settings, out ProjectFileError? error), error?.Message);
				Assert.Equal("Café Mod", settings!.Name);
				Assert.Equal(OptimizationLevel.Full, settings.Optimization);
			}
		}

		[Fact]
		public void Read_OfAFileWithAByteOrderMark_IsLoadedNotDamaged()
		{
			File.WriteAllBytes(this.FilePath, new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("{ \"name\": \"Notepad\" }")).ToArray());

			ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

			Assert.Equal(ProjectFileStatus.Loaded, result.Status);
			Assert.Equal("Notepad", result.Settings!.Name);
			Assert.False(File.Exists(this.FilePath + ".corrupt"));
		}

		[Fact]
		public void TryParse_ResolvesPathsAndToleratesCommentsAndTrailingCommas()
		{
			Byte[] content = System.Text.Encoding.UTF8.GetBytes("{ // comment\n \"outputDirectory\": \"out\", }");

			Assert.True(ProjectSettingsFile.TryParse(content, this.Project, out ProjectSettings? settings, out ProjectFileError? error));

			Assert.Null(error);
			Assert.Equal(Path.Combine(this.Project, "out"), settings!.OutputDirectory);
		}

		[Fact]
		public void Read_ReturnsTheBytesItInterpreted_AndWriteReturnsTheBytesItWrote()
		{
			Byte[] written = ProjectSettingsFile.Write(this.Project, new ProjectSettings { Name = "Mod" });

			ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

			Assert.Equal(written, File.ReadAllBytes(this.FilePath));
			Assert.Equal(written, result.Content);
			Assert.Equal(written, ProjectSettingsFile.Serialize(this.Project, new ProjectSettings { Name = "Mod" }));
			Assert.Null(ProjectSettingsFile.Read(this.Project + "-none").Content);
		}

		[Fact]
		public void Read_WithoutAFile_ReportsMissing()
		{
			ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

			Assert.Equal(ProjectFileStatus.Missing, result.Status);
			Assert.Null(result.Settings);
		}

		[Fact]
		public void Write_StoresThePathsInsideTheProjectRelativeWithForwardSlashes()
		{
			ProjectSettings settings = new ProjectSettings
			{
				HeadersDirectory = Path.Combine(this.Project, "headers"),
				OutputDirectory = Path.Combine(this.Project, "out", "scripts"),
				ScriptsHeaderFile = Path.Combine(this.Project, "headers", "SCRIPTS.H"),
				ScriptsListFile = Path.Combine(this.Project, "data", "scripts.lst"),
				MessageSearchDirectories = new List<String> { Path.Combine(this.Project, "text", "english") },
			};

			ProjectSettingsFile.Write(this.Project, settings);

			String json = File.ReadAllText(this.FilePath);
			Assert.Contains("\"headersDirectory\": \"headers\"", json, StringComparison.Ordinal);
			Assert.Contains("\"outputDirectory\": \"out/scripts\"", json, StringComparison.Ordinal);
			Assert.Contains("\"scriptsHeaderFile\": \"headers/SCRIPTS.H\"", json, StringComparison.Ordinal);
			Assert.Contains("\"scriptsListFile\": \"data/scripts.lst\"", json, StringComparison.Ordinal);
			Assert.Contains("\"text/english\"", json, StringComparison.Ordinal);
			Assert.DoesNotContain(this.Project.Replace('\\', '/'), json, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Read_ResolvesRelativePathsAgainstTheProjectFolder_SoTheFileIsPortable()
		{
			File.WriteAllText(this.FilePath, "{ \"headersDirectory\": \"headers\", \"outputDirectory\": \"../game/scripts\", \"messageSearchDirectories\": [ \"text/english\" ] }");

			ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

			Assert.Equal(ProjectFileStatus.Loaded, result.Status);
			Assert.NotNull(result.Settings);
			Assert.Equal(Path.Combine(this.Project, "headers"), result.Settings.HeadersDirectory);
			Assert.Equal(Path.GetFullPath(Path.Combine(this.Project, "..", "game", "scripts")), result.Settings.OutputDirectory);
			Assert.Equal(Path.Combine(this.Project, "text", "english"), Assert.Single(result.Settings.MessageSearchDirectories!));
		}

		[Fact]
		public void TheSameFileInAnotherFolder_ResolvesToThatFolder()
		{
			ProjectSettings settings = new ProjectSettings { HeadersDirectory = Path.Combine(this.Project, "headers") };
			ProjectSettingsFile.Write(this.Project, settings);
			using TestFolder moved = new TestFolder("project-moved");
			File.Copy(this.FilePath, Path.Combine(moved.Path, ProjectSettings.FileName));

			ProjectFileReadResult result = ProjectSettingsFile.Read(moved.Path);

			Assert.Equal(Path.Combine(moved.Path, "headers"), result.Settings!.HeadersDirectory);
		}

		[Fact]
		public void AbsolutePathsAreKeptAsTheyAre()
		{
			String absolute = Path.GetFullPath(Path.Combine(this.Project, "..", "elsewhere"));
			Assert.Equal(absolute, ProjectSettingsFile.ResolvePath(this.Project, absolute));
			Assert.Equal(Path.Combine(this.Project, "a", "b"), ProjectSettingsFile.ResolvePath(this.Project, "a/b"));
			Assert.Equal(this.Project, ProjectSettingsFile.ResolvePath(this.Project, "."));
			Assert.Equal("../elsewhere", ProjectSettingsFile.ToStoredPath(this.Project, absolute));
		}

		[Fact]
		public void ABlankOverrideIsKept_ItMeansNotSet()
		{
			ProjectSettings settings = new ProjectSettings { OutputDirectory = String.Empty, PreprocessorDefine = String.Empty };
			ProjectSettingsFile.Write(this.Project, settings);

			ProjectSettings read = ProjectSettingsFile.Read(this.Project).Settings!;

			Assert.Equal(String.Empty, read.OutputDirectory);
			Assert.Equal(String.Empty, read.PreprocessorDefine);
			Assert.Null(read.HeadersDirectory);
		}

		[Fact]
		public void UnknownProperties_AreIgnoredWhenReadingAndKeptWhenWriting()
		{
			File.WriteAllText(this.FilePath, "{ \"version\": 1, \"name\": \"Mod\", \"futureOption\": { \"a\": [1, 2] }, \"anotherOne\": \"x\" }");

			ProjectSettings read = ProjectSettingsFile.Read(this.Project).Settings!;
			read.ShortCircuit = true;
			ProjectSettingsFile.Write(this.Project, read);

			String json = File.ReadAllText(this.FilePath);
			Assert.Equal("Mod", read.Name);
			Assert.Contains("futureOption", json, StringComparison.Ordinal);
			Assert.Contains("anotherOne", json, StringComparison.Ordinal);
			Assert.Contains("\"shortCircuit\": true", json, StringComparison.Ordinal);
		}

		[Fact]
		public void CommentsAndTrailingCommas_AreTolerated()
		{
			File.WriteAllText(this.FilePath, "{\n // the project\n \"name\": \"Mod\",\n}");

			ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

			Assert.Equal(ProjectFileStatus.Loaded, result.Status);
			Assert.Equal("Mod", result.Settings!.Name);
		}

		[Fact]
		public void TheVersionIsWritten_AndANewerOneIsNotLowered()
		{
			ProjectSettingsFile.Write(this.Project, new ProjectSettings());
			Assert.Contains("\"version\": " + ProjectSettings.CurrentVersion, File.ReadAllText(this.FilePath), StringComparison.Ordinal);

			File.WriteAllText(this.FilePath, "{ \"version\": 7, \"name\": \"Mod\" }");
			ProjectSettings read = ProjectSettingsFile.Read(this.Project).Settings!;
			ProjectSettingsFile.Write(this.Project, read);

			Assert.Contains("\"version\": 7", File.ReadAllText(this.FilePath), StringComparison.Ordinal);
		}

		[Fact]
		public void ADamagedFile_IsReported_AndACorruptCopyIsKeptNextToIt()
		{
			File.WriteAllText(this.FilePath, "{ \"name\": \"Mod\", ");

			ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

			Assert.Equal(ProjectFileStatus.Damaged, result.Status);
			Assert.Null(result.Settings);
			Assert.Equal("{ \"name\": \"Mod\", ", File.ReadAllText(this.FilePath + ".corrupt"));
			Assert.Equal("{ \"name\": \"Mod\", ", File.ReadAllText(this.FilePath));
		}

		[Fact]
		public void AnEmptyJsonValue_IsDamagedToo()
		{
			File.WriteAllText(this.FilePath, "null");

			Assert.Equal(ProjectFileStatus.Damaged, ProjectSettingsFile.Read(this.Project).Status);
		}

		[Fact]
		public void AFileThatCannotBeRead_IsReportedAndLeftUntouched()
		{
			File.WriteAllText(this.FilePath, "{ \"name\": \"Mod\" }");
			using (new FileStream(this.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				ProjectFileReadResult result = ProjectSettingsFile.Read(this.Project);

				Assert.Equal(ProjectFileStatus.Unreadable, result.Status);
				Assert.Null(result.Settings);
				Assert.False(String.IsNullOrEmpty(result.Detail));
			}

			Assert.Equal("{ \"name\": \"Mod\" }", File.ReadAllText(this.FilePath));
			Assert.False(File.Exists(this.FilePath + ".corrupt"));
		}

		[Fact]
		public void Write_IsAtomic_NoTemporaryFileStaysBehind_AndTheOldContentSurvivesAFailure()
		{
			ProjectSettingsFile.Write(this.Project, new ProjectSettings { Name = "First" });
			Assert.Empty(Directory.GetFiles(this.Project, "*.tmp"));

			using (new FileStream(this.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Exception? failure = Record.Exception(() => ProjectSettingsFile.Write(this.Project, new ProjectSettings { Name = "Second" }));
				Assert.True(failure is IOException or UnauthorizedAccessException);
			}

			Assert.Empty(Directory.GetFiles(this.Project, "*.tmp"));
			Assert.Equal("First", ProjectSettingsFile.Read(this.Project).Settings!.Name);
		}

		[Fact]
		public void Normalize_RepairsWhatWasEditedByHand()
		{
			ProjectSettings settings = new ProjectSettings
			{
				Name = "  ",
				PreprocessorDefine = "<Not Preset>",
				Defines = new List<String> { "A", " A ", "has space", String.Empty, "<none>", "B" },
				MessageSearchDirectories = new List<String> { " C:\\a ", "c:\\A", String.Empty },
			};

			settings.Normalize();

			Assert.Null(settings.Name);
			Assert.Equal(String.Empty, settings.PreprocessorDefine);
			Assert.Equal(new[] { "A", "B" }, settings.Defines);
			Assert.Equal(new[] { "C:\\a" }, settings.MessageSearchDirectories!.ToArray());
		}

		[Fact]
		public void Clone_IsDeep()
		{
			ProjectSettings settings = new ProjectSettings
			{
				Defines = new List<String> { "A" },
				MessageSearchDirectories = new List<String> { "x" },
				Optimization = OptimizationLevel.Full,
			};

			ProjectSettings copy = settings.Clone();
			copy.Defines.Add("B");
			copy.MessageSearchDirectories!.Add("y");

			Assert.Single(settings.Defines);
			Assert.Single(settings.MessageSearchDirectories!);
			Assert.Equal(OptimizationLevel.Full, copy.Optimization);
		}
	}
}
