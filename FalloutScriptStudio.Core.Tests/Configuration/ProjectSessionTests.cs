namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The open documents remembered per project: relative paths, the active document, the limit of 20 projects and the persistence in <c>recent.json</c>.</summary>
	public sealed class ProjectSessionTests : IDisposable
	{
		private readonly TestFolder _root = new TestFolder("project-session");

		public void Dispose()
		{
			this._root.Dispose();
		}

		private String Project => this._root.Combine("mod");

		private String InProject(params String[] parts)
		{
			return Path.Combine(new[] { this.Project }.Concat(parts).ToArray());
		}

		private SettingsService CreateService()
		{
			return new SettingsService(
				AppPaths.FromBaseDirectory(this._root.Combine("app")),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
		}

		[Fact]
		public void Create_StoresPathsRelativeToTheProjectFolder_WithForwardSlashes()
		{
			ProjectSession session = ProjectSession.Create(this.Project, new[] { this.InProject("scripts", "a.ssl"), this.InProject("headers", "b.h") }, this.InProject("headers", "b.h"));

			Assert.Equal(ProjectLocator.Normalize(this.Project), session.Folder);
			Assert.Equal(new[] { "scripts/a.ssl", "headers/b.h" }, session.Documents);
			Assert.Equal("headers/b.h", session.ActiveDocument);
		}

		[Fact]
		public void IsInsideProject_TellsTheFilesOfTheFolderAndItsSubFoldersFromTheOthers()
		{
			Assert.True(ProjectSession.IsInsideProject(this.Project, this.InProject("a.ssl")));
			Assert.True(ProjectSession.IsInsideProject(this.Project, this.InProject("scripts", "deep", "a.ssl")));
			Assert.True(ProjectSession.IsInsideProject(this.Project.ToUpperInvariant(), this.InProject("a.ssl")));
			Assert.False(ProjectSession.IsInsideProject(this.Project, this._root.Combine("other", "a.ssl")));
			Assert.False(ProjectSession.IsInsideProject(this.Project, this._root.Combine("mod2", "a.ssl")));
			Assert.False(ProjectSession.IsInsideProject(this.Project, "a.ssl"));
		}

		[Fact]
		public void Create_IgnoresDocumentsOutsideTheProject_AndDuplicates()
		{
			String outside = this._root.Combine("other", "x.ssl");

			ProjectSession session = ProjectSession.Create(
				this.Project + Path.DirectorySeparatorChar,
				new[] { this.InProject("a.ssl"), outside, this.InProject("A.SSL"), this.Project + "-sibling" + Path.DirectorySeparatorChar + "y.ssl", "relative.ssl" },
				outside);

			Assert.Equal(new[] { "a.ssl" }, session.Documents);
			Assert.Null(session.ActiveDocument);
		}

		[Fact]
		public void TheStoredPathsResolveToTheFullPaths_AndFollowTheFolderWhenItMoves()
		{
			ProjectSession session = ProjectSession.Create(this.Project, new[] { this.InProject("scripts", "a.ssl"), this.InProject("b.ssl") }, this.InProject("b.ssl"));

			Assert.Equal(new[] { this.InProject("scripts", "a.ssl"), this.InProject("b.ssl") }, session.GetDocumentPaths());
			Assert.Equal(this.InProject("b.ssl"), session.GetActiveDocumentPath());

			String moved = this._root.Combine("moved");
			session.Folder = moved;
			Assert.Equal(Path.Combine(moved, "scripts", "a.ssl"), session.GetDocumentPaths()[0]);
		}

		[Fact]
		public void TheActiveDocument_MustBeOneOfTheOpenOnes()
		{
			ProjectSession session = ProjectSession.Create(this.Project, new[] { this.InProject("a.ssl") }, this.InProject("closed.ssl"));

			Assert.Null(session.ActiveDocument);
			Assert.Null(session.GetActiveDocumentPath());
		}

		[Fact]
		public void SetProjectSession_ReplacesThePreviousOneOfTheSameProject_IgnoringCaseAndTrailingSeparators()
		{
			RecentFiles recent = new RecentFiles();
			recent.SetProjectSession(ProjectSession.Create(this.Project, new[] { this.InProject("a.ssl") }, null));
			recent.SetProjectSession(ProjectSession.Create(this.Project.ToUpperInvariant() + Path.DirectorySeparatorChar, new[] { this.InProject("b.ssl").ToUpperInvariant() }, null));

			ProjectSession session = Assert.Single(recent.ProjectSessions);
			Assert.Equal(new[] { "B.SSL" }, session.Documents);
			Assert.NotNull(recent.GetProjectSession(this.Project));
			Assert.NotNull(recent.GetProjectSession(this.Project + Path.DirectorySeparatorChar));
		}

		[Fact]
		public void ASessionWithoutDocuments_ForgetsTheProject()
		{
			RecentFiles recent = new RecentFiles();
			recent.SetProjectSession(ProjectSession.Create(this.Project, new[] { this.InProject("a.ssl") }, null));

			recent.SetProjectSession(ProjectSession.Create(this.Project, Array.Empty<String>(), null));

			Assert.Empty(recent.ProjectSessions);
			Assert.Null(recent.GetProjectSession(this.Project));
		}

		[Fact]
		public void RemoveProjectSession_ForgetsOnlyThatProject()
		{
			RecentFiles recent = new RecentFiles();
			String other = this._root.Combine("other");
			recent.SetProjectSession(ProjectSession.Create(this.Project, new[] { this.InProject("a.ssl") }, null));
			recent.SetProjectSession(ProjectSession.Create(other, new[] { Path.Combine(other, "c.ssl") }, null));

			recent.RemoveProjectSession(this.Project);

			Assert.Null(recent.GetProjectSession(this.Project));
			Assert.NotNull(recent.GetProjectSession(other));
		}

		[Fact]
		public void OnlyTheTwentyNewestProjectsAreRemembered_TheOldestIsDropped()
		{
			RecentFiles recent = new RecentFiles();
			for (Int32 index = 1; index <= RecentFiles.MaxProjectSessions + 3; index++)
			{
				String folder = this._root.Combine("p" + index);
				recent.SetProjectSession(ProjectSession.Create(folder, new[] { Path.Combine(folder, "a.ssl") }, null));
			}

			Assert.Equal(RecentFiles.MaxProjectSessions, recent.ProjectSessions.Count);
			Assert.Null(recent.GetProjectSession(this._root.Combine("p1")));
			Assert.Null(recent.GetProjectSession(this._root.Combine("p3")));
			Assert.NotNull(recent.GetProjectSession(this._root.Combine("p4")));
			Assert.NotNull(recent.GetProjectSession(this._root.Combine("p" + (RecentFiles.MaxProjectSessions + 3))));
		}

		[Fact]
		public void ARememberedProjectThatIsLeftAgainMovesToTheNewest()
		{
			RecentFiles recent = new RecentFiles();
			for (Int32 index = 1; index <= RecentFiles.MaxProjectSessions; index++)
			{
				String folder = this._root.Combine("p" + index);
				recent.SetProjectSession(ProjectSession.Create(folder, new[] { Path.Combine(folder, "a.ssl") }, null));
			}

			String first = this._root.Combine("p1");
			recent.SetProjectSession(ProjectSession.Create(first, new[] { Path.Combine(first, "b.ssl") }, null));
			String extra = this._root.Combine("extra");
			recent.SetProjectSession(ProjectSession.Create(extra, new[] { Path.Combine(extra, "a.ssl") }, null));

			Assert.NotNull(recent.GetProjectSession(first));
			Assert.Null(recent.GetProjectSession(this._root.Combine("p2")));
			Assert.Equal(RecentFiles.MaxProjectSessions, recent.ProjectSessions.Count);
		}

		[Fact]
		public void TheSessionsSurviveSavingAndLoading_AndTheFileHoldsRelativePaths()
		{
			SettingsService first = this.CreateService();
			first.Load();
			first.Recent.SetProjectSession(ProjectSession.Create(this.Project, new[] { this.InProject("scripts", "a.ssl"), this.InProject("b.h") }, this.InProject("b.h")));
			first.Save();
			first.Dispose();

			String json = File.ReadAllText(AppPaths.FromBaseDirectory(this._root.Combine("app")).RecentFilesFile);
			Assert.Contains("scripts/a.ssl", json, StringComparison.Ordinal);
			Assert.Contains("\"b.h\"", json, StringComparison.Ordinal);

			using SettingsService second = this.CreateService();
			second.Load();
			ProjectSession? session = second.Recent.GetProjectSession(this.Project);
			Assert.NotNull(session);
			Assert.Equal(new[] { this.InProject("scripts", "a.ssl"), this.InProject("b.h") }, session.GetDocumentPaths());
			Assert.Equal(this.InProject("b.h"), session.GetActiveDocumentPath());
		}

		[Fact]
		public void ARecentFileWrittenByAnOlderVersion_LoadsWithoutSessions()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(this._root.Combine("app"));
			Directory.CreateDirectory(paths.SettingsDirectory);
			File.WriteAllText(paths.RecentFilesFile, "{ \"scripts\": [ \"C:\\\\a.ssl\" ], \"messages\": [], \"projects\": [ \"C:\\\\mod\" ] }");

			using SettingsService service = this.CreateService();
			service.Load();

			Assert.NotNull(service.Recent.ProjectSessions);
			Assert.Empty(service.Recent.ProjectSessions);
			Assert.Equal(new[] { @"C:\a.ssl" }, service.Recent.Scripts);
			Assert.Null(service.Recent.GetProjectSession(@"C:\mod"));
		}

		[Fact]
		public void ARecentFileWithAnExplicitNullList_LoadsWithoutSessions()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(this._root.Combine("app"));
			Directory.CreateDirectory(paths.SettingsDirectory);
			File.WriteAllText(paths.RecentFilesFile, "{ \"projectSessions\": null }");

			using SettingsService service = this.CreateService();
			service.Load();

			Assert.NotNull(service.Recent.ProjectSessions);
			Assert.Null(service.Recent.GetProjectSession(this.Project));
		}
	}
}