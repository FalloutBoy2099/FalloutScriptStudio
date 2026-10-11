namespace FalloutScriptStudio.Core.Tests.Archives
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Xunit;

	public sealed class DatArchiveReaderTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private static Byte[] Text(String text)
		{
			return Encoding.ASCII.GetBytes(text);
		}

		private static Byte[] Compressible(Int32 length)
		{
			return Text(String.Concat(Enumerable.Repeat("fallout script studio ", length / 22 + 1))).Take(length).ToArray();
		}

		private static Byte[] Noise(Int32 length, Int32 seed)
		{
			Byte[] data = new Byte[length];
			new Random(seed).NextBytes(data);
			return data;
		}

		private String Save(String name, Byte[] archive)
		{
			String path = Path.Combine(this._workspace.Root, name);
			File.WriteAllBytes(path, archive);
			return path;
		}

		private static DatTestArchiveBuilder Sample(out Dictionary<String, Byte[]> expected)
		{
			expected = new Dictionary<String, Byte[]>
			{
				[@"scripts\test.int"] = Compressible(5000),
				[@"scripts\noise.int"] = Noise(700, 1),
				[@"scripts\empty.int"] = Array.Empty<Byte>(),
				[@"data\text.msg"] = Compressible(100),
				["root.cfg"] = Noise(33, 2),
			};

			DatTestArchiveBuilder builder = new DatTestArchiveBuilder();
			builder.AddCompressed(@"scripts\test.int", expected[@"scripts\test.int"]);
			builder.AddStored(@"scripts\noise.int", expected[@"scripts\noise.int"]);
			builder.AddStored(@"scripts\empty.int", expected[@"scripts\empty.int"]);
			builder.AddCompressed(@"data\text.msg", expected[@"data\text.msg"]);
			builder.AddStored("root.cfg", expected["root.cfg"]);
			return builder;
		}

		private static async Task AssertContentAsync(IDatArchiveReader reader, Dictionary<String, Byte[]> expected)
		{
			Assert.Equal(expected.Count, reader.Entries.Count);
			foreach (KeyValuePair<String, Byte[]> pair in expected)
			{
				DatEntry? entry = reader.Find(pair.Key.ToUpperInvariant());
				Assert.NotNull(entry);
				Assert.Equal(pair.Value.Length, entry.UnpackedSize);
				Assert.Equal(pair.Value, await reader.ReadAllBytesAsync(entry));
			}
		}

		[Fact]
		public async Task Fallout2_ReadsStoredAndCompressedFiles()
		{
			DatTestArchiveBuilder builder = Sample(out Dictionary<String, Byte[]> expected);
			String path = this.Save("master.dat", builder.BuildFallout2());

			DatArchiveReader reader = DatArchiveReader.Open(path);

			Assert.Equal(DatFormat.Fallout2, reader.Format);
			Assert.Equal(path, reader.FilePath);
			await AssertContentAsync(reader, expected);
			Assert.True(reader.Find(@"scripts\test.int")!.IsCompressed);
			Assert.False(reader.Find(@"scripts\noise.int")!.IsCompressed);
		}

		[Fact]
		public async Task Fallout1_ReadsStoredAndLzssCompressedFiles()
		{
			DatTestArchiveBuilder builder = Sample(out Dictionary<String, Byte[]> expected);
			String path = this.Save("master.dat", builder.BuildFallout1());

			DatArchiveReader reader = DatArchiveReader.Open(path);

			Assert.Equal(DatFormat.Fallout1, reader.Format);
			await AssertContentAsync(reader, expected);
			Assert.True(reader.Find(@"scripts\test.int")!.IsCompressed);
			Assert.True(reader.Find(@"scripts\test.int")!.PackedSize < 5000);
		}

		[Fact]
		public async Task Fallout1_DecodesAHandMadeBlockAndRawBlocks()
		{
			// Literal 'A', then a match of 5 bytes at dictionary position 4078 (where the literal was stored): "AAAAAA".
			Byte[] matchBlock = [0x00, 0x04, 0x01, (Byte)'A', 0xEE, 0xF2, 0x00, 0x00];
			Byte[] raw = Text("raw bytes");
			String path = this.Save(
				"fo1.dat",
				new DatTestArchiveBuilder()
					.AddRawCompressed(@"data\a.txt", Text("AAAAAA"), matchBlock)
					.AddRawCompressed(@"data\raw.txt", raw, DatTestArchiveBuilder.LzssRawBlock(raw))
					.BuildFallout1());

			DatArchiveReader reader = DatArchiveReader.Open(path);

			Assert.Equal("AAAAAA", Encoding.ASCII.GetString(await reader.ReadAllBytesAsync(reader.Find("data/a.txt")!)));
			Assert.Equal(raw, await reader.ReadAllBytesAsync(reader.Find("data/raw.txt")!));
		}

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public void Lookups_IgnoreCaseAndAcceptSlashes(Int32 generation)
		{
			DatTestArchiveBuilder builder = Sample(out _);
			String path = this.Save("master.dat", generation == 1 ? builder.BuildFallout1() : builder.BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			DatEntry? entry = reader.Find("/SCRIPTS/Test.INT");

			Assert.NotNull(entry);
			Assert.Equal(@"scripts\", entry.Directory);
			Assert.Equal("test.int", entry.Name);
			Assert.Equal(@"scripts\test.int", entry.FullPath);
			Assert.Null(reader.Find(@"scripts\missing.int"));
			Assert.Equal(3, reader.GetEntries("Scripts").Count);
			Assert.Equal(3, reader.GetEntries(@"\scripts\").Count);
			Assert.Single(reader.GetEntries(String.Empty));
			Assert.Empty(reader.GetEntries("nothing"));
		}

		[Fact]
		public void EmptyArchives_AreAccepted()
		{
			DatTestArchiveBuilder builder = new DatTestArchiveBuilder();

			Assert.Empty(DatArchiveReader.Open(this.Save("empty2.dat", builder.BuildFallout2())).Entries);
			Assert.Empty(DatArchiveReader.Open(this.Save("empty1.dat", builder.BuildFallout1())).Entries);
		}

		[Fact]
		public void OpenEntry_OfAStoredFile_IsSeekable()
		{
			Byte[] content = Noise(1000, 5);
			String path = this.Save("master.dat", new DatTestArchiveBuilder().AddStored("a.bin", Noise(77, 3)).AddStored("b.bin", content).BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			using Stream stream = reader.OpenEntry(reader.Find("b.bin")!);

			Assert.True(stream.CanSeek);
			Assert.Equal(1000, stream.Length);
			stream.Seek(990, SeekOrigin.Begin);
			Byte[] tail = new Byte[50];
			Assert.Equal(10, stream.Read(tail, 0, tail.Length));
			Assert.Equal(content[990..], tail[..10]);
			Assert.Equal(0, stream.Read(tail, 0, tail.Length));
			stream.Position = 0;
			Assert.Equal(content[0], stream.ReadByte());
		}

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public void OpenEntry_OfACompressedFile_StreamsInSmallReads(Int32 generation)
		{
			Byte[] content = Compressible(20000);
			DatTestArchiveBuilder builder = new DatTestArchiveBuilder().AddCompressed("big.txt", content);
			String path = this.Save("master.dat", generation == 1 ? builder.BuildFallout1() : builder.BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			using Stream stream = reader.OpenEntry(reader.Find("big.txt")!);
			using MemoryStream copy = new MemoryStream();
			Byte[] buffer = new Byte[333];
			Int32 read;
			while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				copy.Write(buffer, 0, read);
			}

			Assert.Equal(content, copy.ToArray());
		}

		[Fact]
		public async Task Lzss_MatchesReachingAcrossTheWholeDictionary_AreDecoded()
		{
			// Several blocks, runs, and repeated phrases far apart exercise the dictionary wrap-around.
			StringBuilder text = new StringBuilder();
			Random random = new Random(11);
			String[] words = ["procedure", "begin", "end", "variable", "if", "then", "else", "call", "return", "while"];
			while (text.Length < 30000)
			{
				text.Append(words[random.Next(words.Length)]).Append(random.Next(4) == 0 ? "\r\n" : " ");
			}

			Byte[] content = Text(text.ToString()).Concat(new Byte[300]).Concat(Noise(500, 9)).ToArray();
			String path = this.Save("lzss.dat", new DatTestArchiveBuilder().AddCompressed("data/long.txt", content).BuildFallout1());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			Assert.Equal(content, await reader.ReadAllBytesAsync(reader.Find("data/long.txt")!));
		}

		[Fact]
		public void Open_RejectsWhatIsNotAnArchive()
		{
			DatException missing = Assert.Throws<DatException>(() => DatArchiveReader.Open(Path.Combine(this._workspace.Root, "missing.dat")));
			Assert.Equal(DatErrorKind.FileNotFound, missing.Kind);

			DatException empty = Assert.Throws<DatException>(() => DatArchiveReader.Open(this.Save("empty.dat", [])));
			Assert.Equal(DatErrorKind.EmptyFile, empty.Kind);

			DatException text = Assert.Throws<DatException>(() => DatArchiveReader.Open(this.Save("text.dat", Text("this is definitely not an archive, only some text that is long enough"))));
			Assert.Equal(DatErrorKind.NotADat, text.Kind);

			DatException blank = Assert.Throws<DatException>(() => DatArchiveReader.Open(" "));
			Assert.Equal(DatErrorKind.InvalidFileName, blank.Kind);
		}

		[Fact]
		public void Open_RejectsATruncatedOrDamagedDirectory()
		{
			Byte[] fallout2 = Sample(out _).BuildFallout2();
			DatException cut = Assert.Throws<DatException>(() => DatArchiveReader.Open(this.Save("cut2.dat", fallout2[..^3])));
			Assert.True(cut.Kind is DatErrorKind.NotADat or DatErrorKind.TruncatedFile);

			Byte[] damaged = (Byte[])fallout2.Clone();
			Int32 treeStart = damaged.Length - 8 - BitConverter.ToInt32(damaged, damaged.Length - 8);
			damaged[treeStart + 4] = 0x7F;
			Assert.Equal(DatErrorKind.InvalidTree, Assert.Throws<DatException>(() => DatArchiveReader.Open(this.Save("damaged2.dat", damaged))).Kind);

			Byte[] fallout1 = Sample(out _).BuildFallout1();
			Assert.Throws<DatException>(() => DatArchiveReader.Open(this.Save("cut1.dat", fallout1[..40])));
		}

		[Fact]
		public void Open_ReportsArcanumArchivesAsUnsupported()
		{
			Byte[] data = new Byte[64];
			Encoding.ASCII.GetBytes("1TAD").CopyTo(data, data.Length - 12);

			DatException exception = Assert.Throws<DatException>(() => DatArchiveReader.Open(this.Save("arcanum.dat", data)));

			Assert.Equal(DatErrorKind.UnsupportedFormat, exception.Kind);
		}

		[Fact]
		public void OpenEntry_OfAFileOutsideTheArchive_Throws()
		{
			Byte[] archive = new DatTestArchiveBuilder().AddStored("a.bin", Noise(50, 1)).BuildFallout2();
			Int32 treeStart = archive.Length - 8 - BitConverter.ToInt32(archive, archive.Length - 8);

			// Entry layout: count(4), name length(4), name(5), flag(1), unpacked(4), packed(4), offset(4).
			BitConverter.GetBytes(10_000).CopyTo(archive, treeStart + 4 + 4 + 5 + 1);
			BitConverter.GetBytes(10_000).CopyTo(archive, treeStart + 4 + 4 + 5 + 1 + 4);
			DatArchiveReader reader = DatArchiveReader.Open(this.Save("outside.dat", archive));

			DatException exception = Assert.Throws<DatException>(() => reader.OpenEntry(reader.Find("a.bin")!));

			Assert.Equal(DatErrorKind.OutsideArchive, exception.Kind);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public async Task CorruptCompressedData_IsReportedAsADatException(Int32 generation)
		{
			Byte[] content = Compressible(3000);
			Byte[] garbage = Noise(60, 4);
			DatTestArchiveBuilder builder = new DatTestArchiveBuilder().AddRawCompressed("bad.txt", content, garbage);
			String path = this.Save("bad.dat", generation == 1 ? builder.BuildFallout1() : builder.BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			DatException exception = await Assert.ThrowsAsync<DatException>(() => reader.ReadAllBytesAsync(reader.Find("bad.txt")!));

			Assert.Equal(DatErrorKind.CorruptData, exception.Kind);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public async Task TruncatedCompressedData_IsReportedAsCorrupt(Int32 generation)
		{
			Byte[] content = Compressible(3000);
			Byte[] whole = generation == 1 ? DatTestArchiveBuilder.CompressLzss(content) : DatTestArchiveBuilder.CompressZlib(content);
			DatTestArchiveBuilder builder = new DatTestArchiveBuilder().AddRawCompressed("cut.txt", content, whole[..(whole.Length / 2)]);
			String path = this.Save("cut.dat", generation == 1 ? builder.BuildFallout1() : builder.BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			DatException exception = await Assert.ThrowsAsync<DatException>(() => reader.ReadAllBytesAsync(reader.Find("cut.txt")!));

			Assert.Equal(DatErrorKind.CorruptData, exception.Kind);
		}

		[Fact]
		public async Task ExtractToFile_WritesTheFile_AndRemovesAPartialOneOnFailure()
		{
			Byte[] content = Compressible(4000);
			Byte[] whole = DatTestArchiveBuilder.CompressZlib(content);
			String path = this.Save(
				"master.dat",
				new DatTestArchiveBuilder()
					.AddCompressed("ok.txt", content)
					.AddRawCompressed("bad.txt", content, whole[..(whole.Length / 2)])
					.BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);
			String good = Path.Combine(this._workspace.Scratch, "ok.txt");
			String bad = Path.Combine(this._workspace.Scratch, "bad.txt");

			await reader.ExtractToFileAsync(reader.Find("ok.txt")!, good);
			await Assert.ThrowsAsync<DatException>(() => reader.ExtractToFileAsync(reader.Find("bad.txt")!, bad));

			Assert.Equal(content, File.ReadAllBytes(good));
			Assert.False(File.Exists(bad));
		}

		[Fact]
		public async Task Reading_CanBeCancelled()
		{
			String path = this.Save("master.dat", new DatTestArchiveBuilder().AddCompressed("a.txt", Compressible(3000)).BuildFallout1());
			DatArchiveReader reader = DatArchiveReader.Open(path);
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAllBytesAsync(reader.Find("a.txt")!, cancellation.Token));
		}

		[Fact]
		public async Task Streams_AreIndependent()
		{
			Byte[] first = Compressible(2000);
			Byte[] second = Noise(300, 8);
			String path = this.Save("master.dat", new DatTestArchiveBuilder().AddCompressed("a.txt", first).AddStored("b.bin", second).BuildFallout2());
			DatArchiveReader reader = DatArchiveReader.Open(path);

			using Stream a = reader.OpenEntry(reader.Find("a.txt")!);
			using Stream b = reader.OpenEntry(reader.Find("b.bin")!);
			Byte[] bufferA = new Byte[100];
			Byte[] bufferB = new Byte[100];
			Assert.Equal(100, await a.ReadAsync(bufferA));
			Assert.Equal(100, await b.ReadAsync(bufferB));
			Assert.Equal(first[..100], bufferA);
			Assert.Equal(second[..100], bufferB);
		}

		[Fact]
		public void Factory_OpensArchivesAndFolders()
		{
			String archive = this.Save("master.dat", Sample(out _).BuildFallout2());
			String folder = Path.Combine(this._workspace.Root, "data.dat");
			Directory.CreateDirectory(Path.Combine(folder, "scripts"));
			File.WriteAllText(Path.Combine(folder, "scripts", "a.int"), "x");
			DatArchiveReaderFactory factory = new DatArchiveReaderFactory();

			Assert.Equal(DatFormat.Fallout2, factory.Open(archive).Format);
			Assert.Equal(DatFormat.Folder, factory.Open(folder).Format);
			Assert.Equal(DatErrorKind.InvalidFileName, Assert.Throws<DatException>(() => factory.Open(String.Empty)).Kind);
		}

		[Fact]
		public async Task Factory_OpenAsync_ReadsTheArchive()
		{
			String archive = this.Save("master.dat", Sample(out _).BuildFallout1());

			IDatArchiveReader reader = await new DatArchiveReaderFactory().OpenAsync(archive);

			Assert.Equal(5, reader.Entries.Count);
		}

		[Fact]
		public async Task FolderReader_ReadsTheFilesOfAFolderLikeAnArchive()
		{
			String folder = Path.Combine(this._workspace.Root, "master.dat");
			Directory.CreateDirectory(Path.Combine(folder, "scripts", "sub"));
			File.WriteAllText(Path.Combine(folder, "scripts", "Test.int"), "hello");
			File.WriteAllText(Path.Combine(folder, "scripts", "sub", "deep.int"), "deeper");
			File.WriteAllText(Path.Combine(folder, "root.txt"), "r");

			DatFolderReader reader = DatFolderReader.Open(folder);

			Assert.Equal(DatFormat.Folder, reader.Format);
			DatEntry? entry = reader.Find("SCRIPTS/test.int");
			Assert.NotNull(entry);
			Assert.Equal(5, entry.UnpackedSize);
			Assert.Equal("hello", Encoding.ASCII.GetString(await reader.ReadAllBytesAsync(entry)));
			Assert.Equal(3, reader.Entries.Count);
			Assert.Equal(@"scripts\sub\deep.int", reader.Entries.Single(e => e.Name == "deep.int").FullPath);
			Assert.Single(reader.GetEntries("scripts"));
			Assert.Null(reader.Find(@"scripts\none.int"));
			Assert.Null(reader.Find(@"..\master.dat\root.txt"));
			Assert.Null(reader.Find(@"C:\Windows\win.ini"));
			Assert.Equal(DatErrorKind.FileNotFound, Assert.Throws<DatException>(() => DatFolderReader.Open(Path.Combine(this._workspace.Root, "absent"))).Kind);
		}
	}
}
