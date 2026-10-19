namespace FalloutScriptStudio.Core.Tests.Archives
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Tests.Compilation;

	public sealed class DatArchiveExtractorTests : IDisposable
	{
		private static readonly Byte[] ScriptContent = Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat("compiled script ", 200)));

		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private static DatArchiveExtractor CreateExtractor(IDatArchiveReaderFactory? readers = null)
		{
			return new DatArchiveExtractor(NullLogger<DatArchiveExtractor>.Instance, readers);
		}

		private DatExtractionRequest CreateRequest(String datFile, String scriptName = "test.int")
		{
			return new DatExtractionRequest
			{
				DatFilePath = datFile,
				ScriptFileName = scriptName,
				OutputDirectory = this._workspace.Scratch,
			};
		}

		private String CreateArchive(String name, Func<DatTestArchiveBuilder, Byte[]> build)
		{
			DatTestArchiveBuilder builder = new DatTestArchiveBuilder()
				.AddCompressed(@"scripts\test.int", ScriptContent)
				.AddStored(@"scripts\Other.INT", [1, 2, 3])
				.AddStored(@"data\test.int", [9]);
			String path = Path.Combine(this._workspace.Root, name);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, build(builder));
			return path;
		}

		[Fact]
		public async Task Extract_Fallout2_WritesTheScriptToTheOutputDirectory()
		{
			String dat = this.CreateArchive(@"game\master.dat", builder => builder.BuildFallout2());

			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(dat));

			Assert.True(result.Success, result.Message);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "test.int"), result.ExtractedFilePath);
			Assert.Equal(ScriptContent, File.ReadAllBytes(result.ExtractedFilePath!));
		}

		[Fact]
		public async Task Extract_Fallout1_WritesTheScriptToTheOutputDirectory()
		{
			String dat = this.CreateArchive(@"game\master.dat", builder => builder.BuildFallout1());

			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(dat));

			Assert.True(result.Success, result.Message);
			Assert.Equal(ScriptContent, File.ReadAllBytes(result.ExtractedFilePath!));
		}

		[Fact]
		public async Task Extract_FromAFolderInPlaceOfTheArchive()
		{
			String folder = Path.Combine(this._workspace.Root, "game", "master.dat");
			Directory.CreateDirectory(Path.Combine(folder, "scripts"));
			File.WriteAllBytes(Path.Combine(folder, "scripts", "test.int"), ScriptContent);

			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(folder));

			Assert.True(result.Success, result.Message);
			Assert.Equal(ScriptContent, File.ReadAllBytes(result.ExtractedFilePath!));
		}

		[Theory]
		[InlineData("TEST.INT")]
		[InlineData(@"scripts\test.int")]
		[InlineData("scripts/Test.Int")]
		[InlineData(@"C:\whatever\folder\test.int")]
		public async Task Extract_AcceptsNamesWithAnyCaseAndSeparators(String scriptName)
		{
			String dat = this.CreateArchive(@"game\master.dat", builder => builder.BuildFallout2());

			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(dat, scriptName));

			Assert.True(result.Success, result.Message);
			Assert.EndsWith(Path.GetFileName(scriptName.Replace('\\', '/')), result.ExtractedFilePath, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task Extract_OnlyLooksInTheScriptsFolder()
		{
			String dat = this.CreateArchive(@"game\master.dat", builder => builder.BuildFallout2());

			// The archive also has data\test.int, but a script that exists only outside scripts\ is not found.
			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(dat, "text.msg"));

			Assert.False(result.Success);
			Assert.Null(result.ExtractedFilePath);
			Assert.Contains("not found in the archive", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Extract_FailsForAMissingArchive()
		{
			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(Path.Combine(this._workspace.Root, "missing.dat")));

			Assert.False(result.Success);
			Assert.Null(result.ExtractedFilePath);
		}

		[Fact]
		public async Task Extract_FailsForAFileThatIsNotAnArchive()
		{
			String dat = this._workspace.CreateFile(@"game\master.dat", "this is just text, long enough to not look like anything");

			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(dat));

			Assert.False(result.Success);
			Assert.Contains("could not be read", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Extract_FailsForCorruptDataAndLeavesNoFile()
		{
			Byte[] whole = DatTestArchiveBuilder.CompressZlib(ScriptContent);
			String dat = Path.Combine(this._workspace.Root, "bad.dat");
			File.WriteAllBytes(dat, new DatTestArchiveBuilder().AddRawCompressed(@"scripts\test.int", ScriptContent, whole[..10]).BuildFallout2());

			DatExtractionResult result = await CreateExtractor().ExtractScriptAsync(this.CreateRequest(dat));

			Assert.False(result.Success);
			Assert.False(File.Exists(Path.Combine(this._workspace.Scratch, "test.int")));
		}

		[Fact]
		public async Task Extract_CreatesTheOutputDirectory_AndCanBeCancelled()
		{
			String dat = this.CreateArchive(@"game\master.dat", builder => builder.BuildFallout2());
			String output = Path.Combine(this._workspace.Root, "new", "folder");
			DatExtractionRequest request = this.CreateRequest(dat) with { OutputDirectory = output };

			Assert.True((await CreateExtractor().ExtractScriptAsync(request)).Success);
			Assert.True(File.Exists(Path.Combine(output, "test.int")));

			using CancellationTokenSource cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateExtractor().ExtractScriptAsync(request, cancellation.Token));
		}

		[Fact]
		public async Task Extract_UsesTheInjectedReaderFactory()
		{
			FakeReaderFactory factory = new FakeReaderFactory();

			DatExtractionResult result = await CreateExtractor(factory).ExtractScriptAsync(this.CreateRequest(this._workspace.CreateFile("any.dat", "x")));

			Assert.False(result.Success);
			Assert.Equal(1, factory.Opened);
		}

		[Fact]
		public void AddArchives_RegistersTheServices()
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddArchives();
			using ServiceProvider provider = services.BuildServiceProvider();

			Assert.IsType<DatArchiveExtractor>(provider.GetRequiredService<IDatArchiveExtractor>());
			Assert.IsType<DatArchiveReaderFactory>(provider.GetRequiredService<IDatArchiveReaderFactory>());
		}

		[Fact]
		public void TheExtractor_CanBeCreatedByTheContainerWithoutTheFactory()
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton<IDatArchiveExtractor, DatArchiveExtractor>();
			using ServiceProvider provider = services.BuildServiceProvider();

			Assert.IsType<DatArchiveExtractor>(provider.GetRequiredService<IDatArchiveExtractor>());
		}

		private sealed class FakeReaderFactory : IDatArchiveReaderFactory
		{
			public Int32 Opened { get; private set; }

			public IDatArchiveReader Open(String path)
			{
				this.Opened++;
				throw new DatException(DatErrorKind.NotADat, "fake");
			}

			public Task<IDatArchiveReader> OpenAsync(String path, CancellationToken cancellationToken = default)
			{
				return Task.FromResult(this.Open(path));
			}
		}
	}
}