namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class ScratchDirectoryTests : IDisposable
	{
		private readonly String _root = TestFiles.CreateWorkFolder("scratch");

		public void Dispose()
		{
			TestFiles.DeleteWorkFolder(this._root);
		}

		private AppPaths CreatePaths()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(this._root);
			paths.EnsureCreated();
			return paths;
		}

		[Fact]
		public void Prepare_DeletesReadOnlyLeftovers()
		{
			AppPaths paths = this.CreatePaths();
			String readOnly = Path.Combine(paths.ScratchDirectory, "backup.int");
			File.WriteAllText(readOnly, "x");
			File.SetAttributes(readOnly, FileAttributes.ReadOnly);

			IReadOnlyList<String> problems = paths.PrepareScratchDirectory();

			Assert.Empty(problems);
			Assert.False(File.Exists(readOnly));
		}

		[Fact]
		public void Prepare_DoesNotThrowForALockedLeftoverAndDeletesTheOthers()
		{
			AppPaths paths = this.CreatePaths();
			String locked = Path.Combine(paths.ScratchDirectory, "locked.ssl");
			String other = Path.Combine(paths.ScratchDirectory, "other.ssl");
			File.WriteAllText(other, "x");
			File.WriteAllText(locked, "x");

			using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				IReadOnlyList<String> problems = paths.PrepareScratchDirectory();

				Assert.Single(problems);
				Assert.Contains("locked.ssl", problems[0], StringComparison.Ordinal);
			}

			Assert.False(File.Exists(other));
			Assert.True(File.Exists(locked));
		}

		[Fact]
		public void Delete_RemovesTheFolderEvenWithReadOnlyFiles()
		{
			AppPaths paths = this.CreatePaths();
			String readOnly = Path.Combine(paths.ScratchDirectory, "backup.int");
			File.WriteAllText(readOnly, "x");
			File.SetAttributes(readOnly, FileAttributes.ReadOnly);

			IReadOnlyList<String> problems = paths.DeleteScratchDirectory();

			Assert.Empty(problems);
			Assert.False(Directory.Exists(paths.ScratchDirectory));
		}

		[Fact]
		public void Delete_DoesNotThrowForALockedFile()
		{
			AppPaths paths = this.CreatePaths();
			String locked = Path.Combine(paths.ScratchDirectory, "locked.ssl");
			File.WriteAllText(locked, "x");

			using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				IReadOnlyList<String> problems = paths.DeleteScratchDirectory();

				Assert.NotEmpty(problems);
			}
		}
	}
}