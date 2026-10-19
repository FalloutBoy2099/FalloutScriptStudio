namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class AppPathsTests : IDisposable
	{
		private readonly String _root = TestFiles.CreateWorkFolder("paths");

		public void Dispose()
		{
			TestFiles.DeleteWorkFolder(this._root);
		}

		[Fact]
		public void WritableProgramFolderKeepsEverythingNextToTheProgram()
		{
			String program = Path.Combine(this._root, "app");

			AppPaths paths = AppPaths.ForInstallation(program, Path.Combine(this._root, "profile"), _ => true);

			Assert.Equal(Path.Combine(program, "settings"), paths.SettingsDirectory);
			Assert.Equal(Path.Combine(program, "scrTemp"), paths.ScratchDirectory);
			Assert.Equal(Path.Combine(program, "resources", "templates"), paths.TemplatesDirectory);
			Assert.Equal(Path.Combine(program, "sse.log"), paths.LogFile);
		}

		[Fact]
		public void ReadOnlyProgramFolderMovesWritableDataToTheUserProfile()
		{
			String program = Path.Combine(this._root, "app");
			String profile = Path.Combine(this._root, "profile");

			AppPaths paths = AppPaths.ForInstallation(program, profile, _ => false);

			Assert.Equal(program, paths.ProgramDirectory);
			Assert.Equal(Path.Combine(program, "resources"), paths.ResourcesDirectory);
			Assert.Equal(Path.Combine(program, "descriptions"), paths.DescriptionsDirectory);
			Assert.Equal(Path.Combine(profile, "settings"), paths.SettingsDirectory);
			Assert.Equal(Path.Combine(profile, "scrTemp"), paths.ScratchDirectory);
			Assert.Equal(Path.Combine(profile, "templates"), paths.TemplatesDirectory);
			Assert.Equal(Path.Combine(profile, "sse.log"), paths.LogFile);
		}

		[Fact]
		public void ReadOnlyInstallationCopiesTheShippedTemplatesWithoutOverwritingUserOnes()
		{
			String program = Path.Combine(this._root, "app");
			String profile = Path.Combine(this._root, "profile");
			String shipped = Path.Combine(program, "resources", "templates");
			Directory.CreateDirectory(shipped);
			File.WriteAllText(Path.Combine(shipped, "a.ssl"), "shipped a");
			File.WriteAllText(Path.Combine(shipped, "b.ssl"), "shipped b");
			AppPaths paths = AppPaths.ForInstallation(program, profile, _ => false);
			Directory.CreateDirectory(paths.TemplatesDirectory);
			File.WriteAllText(Path.Combine(paths.TemplatesDirectory, "b.ssl"), "user b");

			paths.EnsureCreated();

			Assert.Equal("shipped a", File.ReadAllText(Path.Combine(paths.TemplatesDirectory, "a.ssl")));
			Assert.Equal("user b", File.ReadAllText(Path.Combine(paths.TemplatesDirectory, "b.ssl")));
			Assert.True(Directory.Exists(paths.SettingsDirectory));
			Assert.True(Directory.Exists(paths.ScratchDirectory));
		}

		[Fact]
		public void RealProbeAcceptsAWritableFolderAndLeavesNoFileBehind()
		{
			String program = Path.Combine(this._root, "app");

			AppPaths paths = AppPaths.ForInstallation(program, Path.Combine(this._root, "profile"));

			Assert.Equal(program, paths.UserDataDirectory);
			Assert.Empty(Directory.GetFiles(program));
		}
	}
}