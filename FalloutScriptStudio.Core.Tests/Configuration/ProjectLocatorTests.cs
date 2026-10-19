namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>Finding the project of a script: the nearest folder with a project file, at most five levels up.</summary>
	public sealed class ProjectLocatorTests : IDisposable
	{
		private readonly TestFolder _root = new TestFolder("project-locator");

		public void Dispose()
		{
			this._root.Dispose();
		}

		private String MakeProject(String relativeFolder)
		{
			this._root.Write(Path.Combine(relativeFolder, ProjectSettings.FileName), "{}");
			return Path.Combine(this._root.Path, relativeFolder);
		}

		[Fact]
		public void AScriptInTheProjectFolder_BelongsToIt()
		{
			String project = this.MakeProject("mod");
			String script = this._root.Write(@"mod\gl_a.ssl", "procedure start begin end");

			Assert.Equal(project, ProjectLocator.FindProjectFolder(script));
		}

		[Fact]
		public void AScriptInASubfolder_BelongsToTheProjectAbove()
		{
			String project = this.MakeProject("mod");
			String script = this._root.Write(@"mod\scripts\maps\a.ssl", "procedure start begin end");

			Assert.Equal(project, ProjectLocator.FindProjectFolder(script));
		}

		[Fact]
		public void TheNearestProjectWins()
		{
			this.MakeProject("mod");
			String inner = this.MakeProject(@"mod\sub");
			String script = this._root.Write(@"mod\sub\deep\a.ssl", "x");

			Assert.Equal(inner, ProjectLocator.FindProjectFolder(script));
		}

		[Fact]
		public void TheSearchStopsAfterFiveLevels()
		{
			String project = this.MakeProject("mod");
			String fifth = this._root.Write(@"mod\a\b\c\d\x.ssl", "x");
			String sixth = this._root.Write(@"mod\a\b\c\d\e\x.ssl", "x");

			// The folder of the script and the four above it are examined: mod is the fifth folder from mod\a\b\c\d, the sixth is out of reach.
			Assert.Equal(project, ProjectLocator.FindProjectFolder(fifth));
			Assert.Null(ProjectLocator.FindProjectFolder(sixth));
		}

		[Fact]
		public void AScriptOutsideEveryProject_BelongsToNone()
		{
			this.MakeProject("mod");
			String script = this._root.Write(@"other\a.ssl", "x");

			Assert.Null(ProjectLocator.FindProjectFolder(script));
		}

		[Fact]
		public void AFolderPath_IsExaminedItself()
		{
			String project = this.MakeProject("mod");

			Assert.Equal(project, ProjectLocator.FindProjectFolder(project));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("a<b>:|c")]
		public void APathThatCannotBeExamined_BelongsToNone(String? path)
		{
			Assert.Null(ProjectLocator.FindProjectFolder(path));
		}

		[Fact]
		public void ADriveRootIsReachedWithoutFailing()
		{
			Assert.Null(ProjectLocator.FindProjectFolder(Path.GetPathRoot(this._root.Path), 50));
		}

		[Fact]
		public void Normalize_RemovesTheTrailingSeparatorButKeepsADriveRoot()
		{
			Assert.Equal(this._root.Path, ProjectLocator.Normalize(this._root.Path + Path.DirectorySeparatorChar));
			String root = Path.GetPathRoot(this._root.Path)!;
			Assert.Equal(root, ProjectLocator.Normalize(root));
		}
	}
}