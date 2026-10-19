namespace FalloutScriptStudio.Core.Tests.IO
{
	using System;
	using System.IO;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The background readers of the sources share the file with everybody: a program that writes, replaces or deletes it is never kept out.</summary>
	public sealed class SharedFileTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("shared-file");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		[Fact]
		public void ReadAllBytes_ReadsAFileThatAnotherProgramHasOpenForWriting()
		{
			String path = this._folder.Write("a.ssl", "first");
			using FileStream writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);

			// The framework reader refuses: it asks for FileShare.Read, which the open writer does not allow.
			Assert.Throws<IOException>(() => File.ReadAllBytes(path));
			Assert.Equal(Encoding.ASCII.GetBytes("first"), SharedFile.ReadAllBytes(path));
		}

		[Fact]
		public async Task ReadAllBytesAsync_ReadsAFileThatAnotherProgramHasOpenForWriting()
		{
			String path = this._folder.Write("a.ssl", "first");
			using FileStream writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);

			Assert.Equal(Encoding.ASCII.GetBytes("first"), await SharedFile.ReadAllBytesAsync(path));
		}

		[Fact]
		public void ReadAllText_UsesTheEncodingOnlyWhenThereIsNoByteOrderMark()
		{
			String ansi = this._folder.Combine("ansi.ssl");
			File.WriteAllBytes(ansi, TextEncodings.Ansi.GetBytes("café"));
			String utf8 = this._folder.Combine("utf8.ssl");
			File.WriteAllBytes(utf8, [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes("café 中")]);

			Assert.Equal("café", SharedFile.ReadAllText(ansi, TextEncodings.Ansi));
			Assert.Equal("café 中", SharedFile.ReadAllText(utf8, TextEncodings.Ansi));
		}

		[Fact]
		public async Task ReadAllTextAsync_ReadsTheTextOfAFileThatIsOpenForWriting()
		{
			String path = this._folder.Write("a.h", "#define X 1");
			using FileStream writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);

			Assert.Equal("#define X 1", await SharedFile.ReadAllTextAsync(path, Encoding.UTF8));
		}

		[Fact]
		public void AMissingFile_IsAFileNotFoundException_LikeTheFrameworkReaders()
		{
			String path = this._folder.Combine("missing.ssl");

			Assert.Throws<FileNotFoundException>(() => SharedFile.ReadAllBytes(path));
			Assert.Throws<FileNotFoundException>(() => SharedFile.ReadAllText(path, Encoding.UTF8));
		}
	}
}