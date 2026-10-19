namespace FalloutScriptStudio.Core.Tests.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The snippet files and the service that merges them: persistence, unknown properties, damaged and unreadable files, projects.</summary>
	public sealed class SnippetStorageTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private SettingsService CreateSettings()
		{
			SettingsService settings = new SettingsService(
				AppPaths.FromBaseDirectory(this._workspace.Root),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			settings.Load();
			return settings;
		}

		private String File(String name) => Path.Combine(this._workspace.Root, name);

		[Fact]
		public void AFileIsWrittenAsLines_AndReadBackWithTheSameContent()
		{
			String path = this.File("a.json");
			SnippetFile file = new SnippetFile();
			file.Snippets.Add(new SnippetDefinition { Trigger = "ifb", Description = "if block", Body = "if (${1:c}) then begin\n\t$0\nend" });
			file.Snippets.Add(new SnippetDefinition { Trigger = "one", Description = "one line", Body = "x := 1;", Enabled = false });

			SnippetFileStore.Write(path, file);
			SnippetFileReadResult read = SnippetFileStore.Read(path);

			String json = System.IO.File.ReadAllText(path);
			Assert.Contains("\"if (${1:c}) then begin\",", json, StringComparison.Ordinal);
			Assert.Contains("\"body\": \"x := 1;\"", json, StringComparison.Ordinal);
			Assert.Equal(SnippetFileStatus.Loaded, read.Status);
			Assert.Equal(SnippetFile.CurrentVersion, read.File.Version);
			Assert.Equal(2, read.File.Snippets.Count);
			Assert.Equal("if (${1:c}) then begin\n\t$0\nend", read.File.Snippets[0].Body);
			Assert.True(read.File.Snippets[0].Enabled);
			Assert.False(read.File.Snippets[1].Enabled);
			Assert.Empty(Directory.GetFiles(this._workspace.Root, "*.tmp"));
		}

		[Fact]
		public void TheBodyCanBeAnArrayOrAText_CommentsAndTrailingCommasAreTolerated_AndABomIsSkipped()
		{
			String json = "// my snippets\r\n{ \"version\": 1, \"snippets\": [ { \"trigger\": \"a\", \"body\": [\"x\", \"\\ty\"], }, { \"trigger\": \"b\", \"body\": \"one\\ntwo\" }, ], }";
			Byte[] content = new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();

			Boolean ok = SnippetFileStore.TryParse(content, out SnippetFile? file, out String? error);

			Assert.True(ok, error);
			Assert.Equal("x\n\ty", file!.Snippets[0].Body);
			Assert.Equal("one\ntwo", file.Snippets[1].Body);
			Assert.True(file.Snippets[0].Enabled);
		}

		[Fact]
		public void UnknownPropertiesAreKept_ByTheFileAndByEachSnippet()
		{
			String json = "{ \"version\": 3, \"future\": { \"x\": 1 }, \"snippets\": [ { \"trigger\": \"a\", \"body\": \"b\", \"language\": \"ssl\" } ] }";
			Assert.True(SnippetFileStore.TryParse(Encoding.UTF8.GetBytes(json), out SnippetFile? file, out _));

			String written = Encoding.UTF8.GetString(SnippetFileStore.Serialize(file!));

			Assert.Contains("\"future\"", written, StringComparison.Ordinal);
			Assert.Contains("\"language\": \"ssl\"", written, StringComparison.Ordinal);
			Assert.Contains("\"version\": 3", written, StringComparison.Ordinal);
		}

		[Theory]
		[InlineData("")]
		[InlineData("null")]
		[InlineData("[1, 2]")]
		[InlineData("{ \"snippets\": [ { \"trigger\": 5 } ] }")]
		[InlineData("{ \"snippets\": [ { \"body\": [1] } ] }")]
		[InlineData("{ \"snippets\": ")]
		public void ADamagedFile_IsReportedAndCopied_AndNeverThrows(String content)
		{
			String path = this.File("bad.json");
			System.IO.File.WriteAllText(path, content);

			SnippetFileReadResult read = SnippetFileStore.Read(path);

			Assert.Equal(SnippetFileStatus.Damaged, read.Status);
			Assert.Empty(read.File.Snippets);
			Assert.NotNull(read.Error);
			Assert.True(System.IO.File.Exists(path + ".corrupt"));
			Assert.Equal(content, System.IO.File.ReadAllText(path));
		}

		[Fact]
		public void AMissingFile_IsEmpty()
		{
			SnippetFileReadResult read = SnippetFileStore.Read(this.File("none.json"));

			Assert.Equal(SnippetFileStatus.Missing, read.Status);
			Assert.Empty(read.File.Snippets);
		}

		[Fact]
		public void TheService_MergesTheBuiltInUserAndProjectSnippets()
		{
			SettingsService settings = this.CreateSettings();
			SnippetService service = new SnippetService(settings);
			SnippetFile user = new SnippetFile();
			user.Snippets.Add(new SnippetDefinition { Trigger = "ifb", Description = "my if", Body = "if (1) then begin\nend" });
			user.Snippets.Add(new SnippetDefinition { Trigger = "mine", Description = "mine", Body = "x" });
			user.Snippets.Add(new SnippetDefinition { Trigger = "node", Enabled = false });
			SnippetFileStore.Write(service.UserFilePath, user);
			String project = this.File("mod");
			Directory.CreateDirectory(project);
			SnippetFile projectFile = new SnippetFile();
			projectFile.Snippets.Add(new SnippetDefinition { Trigger = "MINE", Description = "project", Body = "y" });
			SnippetFileStore.Write(Path.Combine(project, SnippetFile.ProjectFileName), projectFile);
			settings.Update(s => s.Paths.SolutionFolder = project);

			IReadOnlyList<Snippet> snippets = service.GetSnippets();

			Assert.Equal("my if", snippets.Single(snippet => snippet.Trigger == "ifb").Description);
			Assert.Equal(SnippetSource.User, snippets.Single(snippet => snippet.Trigger == "ifb").Source);
			Assert.Equal("project", snippets.Single(snippet => snippet.Trigger == "MINE").Description);
			Assert.Equal(SnippetSource.Project, snippets.Single(snippet => snippet.Trigger == "MINE").Source);
			Assert.DoesNotContain(snippets, snippet => snippet.Trigger == "node");
			Assert.Contains(snippets, snippet => snippet.Trigger == "procedure" && snippet.Source == SnippetSource.BuiltIn);
			Assert.Equal(snippets.Select(snippet => snippet.Trigger).OrderBy(t => t, StringComparer.OrdinalIgnoreCase), snippets.Select(snippet => snippet.Trigger));
		}

		[Fact]
		public void TheService_FollowsTheProject_AndNoticesAChangeOfAFile()
		{
			SettingsService settings = this.CreateSettings();
			SnippetService service = new SnippetService(settings);
			String first = this.File("first");
			String second = this.File("second");
			Directory.CreateDirectory(first);
			Directory.CreateDirectory(second);
			SnippetFile one = new SnippetFile();
			one.Snippets.Add(new SnippetDefinition { Trigger = "one", Body = "1" });
			SnippetFileStore.Write(Path.Combine(first, SnippetFile.ProjectFileName), one);
			Int32 changes = 0;
			service.Changed += (_, _) => changes++;

			settings.Update(s => s.Paths.SolutionFolder = first);
			Assert.Contains(service.GetSnippets(), snippet => snippet.Trigger == "one");
			settings.Update(s => s.Paths.SolutionFolder = second);
			Assert.DoesNotContain(service.GetSnippets(), snippet => snippet.Trigger == "one");
			Assert.True(changes >= 1);

			SnippetFile edited = new SnippetFile();
			edited.Snippets.Add(new SnippetDefinition { Trigger = "added", Body = "2" });
			SnippetFileStore.Write(Path.Combine(second, SnippetFile.ProjectFileName), edited);
			Assert.Contains(service.GetSnippets(), snippet => snippet.Trigger == "added");
			Assert.Equal(Path.Combine(second, SnippetFile.ProjectFileName), service.ProjectFilePath);
		}

		[Fact]
		public void Save_WritesTheFile_KeepsTheUnknownProperties_AndRaisesChanged()
		{
			SettingsService settings = this.CreateSettings();
			SnippetService service = new SnippetService(settings);
			System.IO.File.WriteAllText(service.UserFilePath, "{ \"version\": 1, \"note\": \"keep me\", \"snippets\": [] }");
			Int32 changes = 0;
			service.Changed += (_, _) => changes++;

			Boolean saved = service.TrySave(SnippetSource.User, new[] { new SnippetDefinition { Trigger = "mine", Description = "d", Body = "b" } }, out String? error);

			Assert.True(saved, error);
			Assert.Equal(1, changes);
			Assert.Contains("keep me", System.IO.File.ReadAllText(service.UserFilePath), StringComparison.Ordinal);
			Assert.Contains(service.GetSnippets(), snippet => snippet.Trigger == "mine");
		}

		[Fact]
		public void Save_NeverCreatesAFileForNothing_AndNeedsAProjectForTheProjectFile()
		{
			SettingsService settings = this.CreateSettings();
			SnippetService service = new SnippetService(settings);

			Assert.True(service.TrySave(SnippetSource.User, Array.Empty<SnippetDefinition>(), out _));
			Assert.False(System.IO.File.Exists(service.UserFilePath));
			Assert.False(service.TrySave(SnippetSource.Project, new[] { new SnippetDefinition { Trigger = "x", Body = "y" } }, out String? error));
			Assert.NotNull(error);
			Assert.False(service.TrySave(SnippetSource.BuiltIn, Array.Empty<SnippetDefinition>(), out _));
		}

		[Fact]
		public void AnUnreadableFile_IsNeverOverwritten()
		{
			SettingsService settings = this.CreateSettings();
			SnippetService service = new SnippetService(settings);
			System.IO.File.WriteAllText(service.UserFilePath, "{ \"snippets\": [ { \"trigger\": \"locked\", \"body\": \"x\" } ] }");

			using (FileStream held = new FileStream(service.UserFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				Assert.Equal(SnippetFileStatus.Unreadable, service.GetFile(SnippetSource.User).Status);
				Boolean saved = service.TrySave(SnippetSource.User, new[] { new SnippetDefinition { Trigger = "new", Body = "y" } }, out String? error);

				Assert.False(saved);
				Assert.NotNull(error);
				Assert.NotNull(held);
			}

			Assert.Contains("locked", System.IO.File.ReadAllText(service.UserFilePath), StringComparison.Ordinal);
			Assert.Contains(service.GetSnippets(), snippet => snippet.Trigger == "procedure");
		}

		[Fact]
		public void ADamagedUserFile_GivesTheBuiltInSnippets_AndIsReplacedWhenTheUserSaves()
		{
			SettingsService settings = this.CreateSettings();
			SnippetService service = new SnippetService(settings);
			System.IO.File.WriteAllText(service.UserFilePath, "{ not json");

			Assert.Equal(SnippetFileStatus.Damaged, service.GetFile(SnippetSource.User).Status);
			Assert.Equal(BuiltInSnippets.All.Count, service.GetSnippets().Count);
			Assert.True(service.TrySave(SnippetSource.User, new[] { new SnippetDefinition { Trigger = "fresh", Body = "z" } }, out _));

			Assert.Equal(SnippetFileStatus.Loaded, service.GetFile(SnippetSource.User).Status);
			Assert.True(System.IO.File.Exists(service.UserFilePath + ".corrupt"));
		}
	}
}