namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>What the project index covers and when it is brought up to date.</summary>
	public sealed class ProjectSymbolServiceTests
	{
		[Fact]
		public void WithAProject_TheRootsAreTheProjectFolderAndTheHeadersFolder()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n");
				String headers = Path.Combine(harness.Folder.Path, "headers");
				Directory.CreateDirectory(headers);
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				await harness.OpenProjectAsync("project");
				String outside = harness.Write("elsewhere/b.ssl", "procedure other begin\r\nend\r\n");
				await harness.OpenAsync(outside);

				ProjectIndexRoots roots = harness.Symbols.GetRoots();

				Assert.Equal(
					new[] { (Path.GetFullPath(harness.Folder.Combine("project")), true), (Path.GetFullPath(headers), true) },
					roots.Folders.Select(folder => (folder.Path, folder.Recursive)).ToArray());
				Assert.Equal(new[] { outside }, roots.Files.ToArray());
			});
		}

		[Fact]
		public void WithoutAProject_TheRootsAreTheFoldersOfTheOpenScripts_AndAMissingHeadersFolderIsIgnored()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = harness.Folder.Combine("no-such-folder"));
				String first = harness.Write("one/a.ssl", "procedure a begin\r\nend\r\n");
				String second = harness.Write("one/b.ssl", "procedure b begin\r\nend\r\n");
				String third = harness.Write("two/c.h", "#define C 1\r\n");
				harness.Write("three/ignored.msg", "{100}{}{x}\r\n");
				await harness.OpenAsync(first);
				await harness.OpenAsync(second);
				await harness.OpenAsync(third);
				await harness.OpenAsync(harness.Write("three/ignored.msg", "{100}{}{x}\r\n"));

				ProjectIndexRoots roots = harness.Symbols.GetRoots();

				Assert.Equal(
					new[] { Path.GetDirectoryName(first)!, Path.GetDirectoryName(third)! }.Select(Path.GetFullPath).ToArray(),
					roots.Folders.Select(folder => folder.Path).ToArray());
				Assert.All(roots.Folders, folder => Assert.False(folder.Recursive));
				Assert.Equal(new[] { first, second, third }, roots.Files.ToArray());
			});
		}

		[Fact]
		public void RefreshingIndexesTheProject()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n");
				harness.Write("project/sub/b.h", "#define LIMIT 5\r\n");
				await harness.OpenProjectAsync("project");

				Assert.True(harness.Workspace.HasProjectSymbol("start"));
				Assert.True(harness.Workspace.HasProjectSymbol("LIMIT"));
				Assert.Equal(2, harness.Index.FileCount);
			});
		}

		[Fact]
		public void ALongRunIsReportedInTheStatusBarAndAShortOneIsNot()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				for (Int32 i = 0; i < 30; i++)
				{
					harness.Write("project/f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl", "procedure p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n");
				}

				await harness.OpenProjectAsync("project");
				Boolean reported = await Eventually.TrueAsync(() => harness.Output.Statuses.Any(status => status.StartsWith("Indexed ", StringComparison.Ordinal)));

				Assert.True(reported);
				Assert.Contains(harness.Output.Statuses, status => status == "Indexed 30 symbols in 30 files");
				Assert.Contains(harness.Output.Statuses, status => status.StartsWith("Indexing ", StringComparison.Ordinal) && status.EndsWith("/30", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void AShortRunSaysNothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n");

				await harness.OpenProjectAsync("project");
				await Task.Delay(100);

				Assert.DoesNotContain(harness.Output.Statuses, status => status.StartsWith("Index", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void OpeningAnotherProjectIndexesItInTheBackground()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("one/a.ssl", "procedure from_one begin\r\nend\r\n");
				harness.Write("two/b.ssl", "procedure from_two begin\r\nend\r\n");
				await harness.OpenProjectAsync("one");
				Assert.True(harness.Workspace.HasProjectSymbol("from_one"));

				harness.Projects.Open(harness.Folder.Combine("two"));

				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.HasProjectSymbol("from_two") && !harness.Workspace.HasProjectSymbol("from_one")));
			});
		}

		[Fact]
		public void SavingAScriptUpdatesItsSymbols()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				document.TextDocument.Text = "procedure start begin\r\nend\r\n\r\nprocedure brand_new begin\r\nend\r\n";
				Assert.True(await harness.Workspace.SaveAsync(document));

				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.HasProjectSymbol("brand_new")));
			});
		}

		[Fact]
		public void OpeningAScriptOutsideTheFoldersAddsItWhenThereIsNoProject()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("loose/a.ssl", "procedure loose_proc begin\r\nend\r\n");

				await harness.OpenAsync(path);

				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.HasProjectSymbol("loose_proc")));
			});
		}

		[Fact]
		public void TheCandidatesOfAProcedureNameAreOrderedByCloseness()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String current = harness.Write("project/scripts/a.ssl", "#include \"..\\headers\\h.h\"\r\nprocedure start begin\r\nend\r\n");
				String header = harness.Write("project/headers/h.h", "#define SAME 1\r\n");
				String sibling = harness.Write("project/scripts/b.h", "#define SAME 2\r\n");
				String far = harness.Write("project/z/c.h", "#define SAME 3\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(current);

				System.Collections.Generic.IReadOnlyList<IndexedSymbol> ordered = harness.Symbols.FindCandidates("SAME", IndexedSymbolKind.All, document);

				Assert.Equal(new[] { header, sibling, far }, ordered.Select(symbol => symbol.File).ToArray());
			});
		}

		[Fact]
		public void TheFileEncodingOfTheSettingsIsUsedToReadTheFiles()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = Path.Combine(harness.Folder.Path, "project", "cyr.ssl");
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllBytes(path, FalloutScriptStudio.Core.IO.TextEncodings.Oem866.GetBytes("#define привет 1\r\n"));
				File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
				harness.Settings.Update(settings => settings.Editor.FileEncoding = FalloutScriptStudio.Core.Configuration.TextEncodingKind.Oem866);
				await harness.OpenProjectAsync("project");

				Assert.True(harness.Workspace.HasProjectSymbol("привет"));
			});
		}
	}
}
