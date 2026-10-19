namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Text;

	/// <summary>
	/// Reads a Fallout 1 or Fallout 2 <c>.dat</c> archive. The directory is read when the archive is opened; no file handle is kept,
	/// and every <see cref="OpenEntry"/> streams the data of one file from its own handle, so it is safe to use from several threads.
	/// </summary>
	/// <remarks>
	/// <para>Fallout 2: <c>[file data][file count][entries][tree size][archive size]</c>, little endian. The last 8 bytes hold the size
	/// of the directory tree and the size of the whole file. Compressed files are zlib streams.</para>
	/// <para>Fallout 1: <c>[header][directory names][directory contents][file data]</c>, big endian. Compressed files are LZSS blocks.</para>
	/// <para>See https://falloutmods.fandom.com/wiki/DAT_file_format. Arcanum archives are not supported.</para>
	/// </remarks>
	public sealed class DatArchiveReader : IDatArchiveReader
	{
		private const Int32 Fallout2FooterSize = 8;
		private const Int32 Fallout1HeaderSize = 16;
		private const Int32 MaxFallout1Directories = 0xFFFFF;
		private const Int32 ArcanumTailSize = 12;
		private const UInt32 ArcanumGuidMagic = 0x44415431;
		private const UInt32 ArcanumPlainMagic = 0x44415420;

		/// <summary>The smallest entry of a Fallout 2 directory tree: a name length, a one byte name, the flag and three numbers.</summary>
		private const Int32 MinimumFallout2EntrySize = 4 + 1 + 13;

		/// <summary>DAT files store single byte characters.</summary>
		private static readonly Encoding NameEncoding = Encoding.Latin1;

		private readonly DatEntryIndex _index = new DatEntryIndex();

		/// <summary>The end of the area of the file data: the start of the directory.</summary>
		private readonly Int64 _dataLimit;

		private DatArchiveReader(String filePath, DatFormat format, Int64 dataLimit)
		{
			this.FilePath = filePath;
			this.Format = format;
			this._dataLimit = dataLimit;
		}

		/// <inheritdoc/>
		public String FilePath { get; }

		/// <inheritdoc/>
		public DatFormat Format { get; }

		/// <inheritdoc/>
		public IReadOnlyList<DatEntry> Entries => this._index.Entries;

		/// <summary>
		/// Opens an archive and reads its directory. The format is detected from the content.
		/// </summary>
		/// <param name="filePath">The <c>.dat</c> file.</param>
		/// <returns>The reader.</returns>
		/// <exception cref="DatException">The file does not exist, cannot be read, or is not a valid Fallout 1 or Fallout 2 archive.</exception>
		public static DatArchiveReader Open(String filePath)
		{
			if (String.IsNullOrWhiteSpace(filePath))
			{
				throw new DatException(DatErrorKind.InvalidFileName, "The archive name is empty.");
			}

			try
			{
				using FileStream stream = OpenStream(filePath, FileOptions.SequentialScan);
				return Read(filePath, stream);
			}
			catch (FileNotFoundException exception)
			{
				throw new DatException(DatErrorKind.FileNotFound, "The archive was not found: " + filePath, exception, filePath);
			}
			catch (DirectoryNotFoundException exception)
			{
				throw new DatException(DatErrorKind.FileNotFound, "The archive was not found: " + filePath, exception, filePath);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				throw new DatException(DatErrorKind.Io, exception.Message, exception, filePath);
			}
		}

		/// <inheritdoc/>
		public DatEntry? Find(String path)
		{
			return this._index.Find(path);
		}

		/// <inheritdoc/>
		public IReadOnlyList<DatEntry> GetEntries(String directory)
		{
			return this._index.GetEntries(directory);
		}

		/// <inheritdoc/>
		public Stream OpenEntry(DatEntry entry)
		{
			ArgumentNullException.ThrowIfNull(entry);

			Int32 size = entry.IsCompressed ? entry.PackedSize : entry.UnpackedSize;
			if (entry.UnpackedSize < 0 || size < 0 || entry.Offset + (Int64)size > this._dataLimit)
			{
				throw new DatException(DatErrorKind.OutsideArchive, "The file '" + entry.FullPath + "' is outside of the archive.", entry.FullPath);
			}

			FileStream file;
			try
			{
				file = OpenStream(this.FilePath, FileOptions.Asynchronous | FileOptions.SequentialScan);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				throw new DatException(DatErrorKind.Io, exception.Message, exception, this.FilePath);
			}

			DatBoundedStream stored = new DatBoundedStream(file, entry.Offset, size);
			if (!entry.IsCompressed)
			{
				return stored;
			}

			Stream decoder = this.Format == DatFormat.Fallout1
				? new LzssDecompressStream(stored)
				: new ZLibStream(stored, CompressionMode.Decompress);
			return new DatInflatedStream(decoder, entry.UnpackedSize, entry.FullPath);
		}

		private static FileStream OpenStream(String filePath, FileOptions options)
		{
			return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, options);
		}

		private static DatArchiveReader Read(String filePath, FileStream stream)
		{
			Int64 length = stream.Length;
			if (length == 0)
			{
				throw new DatException(DatErrorKind.EmptyFile, "The DAT file is empty.", filePath);
			}

			try
			{
				if (IsArcanum(stream, length))
				{
					throw new DatException(DatErrorKind.UnsupportedFormat, "Arcanum archives are not supported.", filePath);
				}

				if (length >= Fallout2FooterSize && TryReadFallout2Footer(stream, length, out Int32 treeSize))
				{
					return ReadFallout2(filePath, stream, treeSize, length);
				}

				if (length >= Fallout1HeaderSize)
				{
					Byte[] header = new Byte[4];
					stream.Position = 0;
					stream.ReadExactly(header);
					Int32 directoryCount = BinaryPrimitives.ReadInt32BigEndian(header);

					// An archive without directories is only the 16 bytes header.
					if ((directoryCount > 0 && directoryCount <= MaxFallout1Directories) || (directoryCount == 0 && length == Fallout1HeaderSize))
					{
						return ReadFallout1(filePath, stream, directoryCount, length);
					}
				}
			}
			catch (EndOfStreamException exception)
			{
				throw new DatException(DatErrorKind.TruncatedFile, "The DAT file is truncated or corrupted.", exception, filePath);
			}

			throw new DatException(DatErrorKind.NotADat, "The file is not a valid DAT archive.", filePath);
		}

		private static Boolean IsArcanum(FileStream stream, Int64 length)
		{
			if (length < ArcanumTailSize)
			{
				return false;
			}

			Byte[] tail = new Byte[4];
			stream.Position = length - ArcanumTailSize;
			stream.ReadExactly(tail);
			UInt32 magic = BinaryPrimitives.ReadUInt32LittleEndian(tail);
			return magic == ArcanumGuidMagic || magic == ArcanumPlainMagic;
		}

		/// <summary>Reads the footer of a Fallout 2 archive; the archive size it holds must be the size of the file.</summary>
		private static Boolean TryReadFallout2Footer(FileStream stream, Int64 length, out Int32 treeSize)
		{
			Byte[] footer = new Byte[Fallout2FooterSize];
			stream.Position = length - Fallout2FooterSize;
			stream.ReadExactly(footer);

			treeSize = BinaryPrimitives.ReadInt32LittleEndian(footer);
			return BinaryPrimitives.ReadUInt32LittleEndian(footer.AsSpan(4)) == length;
		}

		private static DatArchiveReader ReadFallout2(String filePath, FileStream stream, Int32 treeSize, Int64 length)
		{
			if (treeSize < 4 || treeSize + (Int64)Fallout2FooterSize > length)
			{
				throw new DatException(DatErrorKind.InvalidTree, "The directory tree of the DAT file is invalid.", filePath);
			}

			Int64 dataLimit = length - treeSize - Fallout2FooterSize;
			Byte[] tree = new Byte[treeSize];
			stream.Position = dataLimit;
			stream.ReadExactly(tree);

			DatArchiveReader archive = new DatArchiveReader(filePath, DatFormat.Fallout2, dataLimit);
			Int32 fileCount = BinaryPrimitives.ReadInt32LittleEndian(tree);
			if (fileCount < 0 || fileCount > treeSize / MinimumFallout2EntrySize + 1)
			{
				throw new DatException(DatErrorKind.InvalidTree, "The directory tree of the DAT file is damaged.", filePath);
			}

			Int32 position = 4;
			for (Int32 index = 0; index < fileCount; index++)
			{
				if (!TryReadFallout2Entry(tree, ref position, out DatEntry? entry))
				{
					throw new DatException(DatErrorKind.InvalidTree, "The directory tree of the DAT file is damaged.", filePath);
				}

				archive._index.Add(entry);
			}

			return archive;
		}

		private static Boolean TryReadFallout2Entry(Byte[] tree, ref Int32 position, out DatEntry entry)
		{
			entry = null!;
			if (position + 4 > tree.Length)
			{
				return false;
			}

			Int32 nameLength = BinaryPrimitives.ReadInt32LittleEndian(tree.AsSpan(position));
			if (nameLength <= 0 || (Int64)position + 4 + nameLength + 13 > tree.Length)
			{
				return false;
			}

			position += 4;
			String path = NameEncoding.GetString(tree, position, nameLength);
			position += nameLength;

			Boolean compressed = tree[position++] == 1;
			Int32 unpackedSize = BinaryPrimitives.ReadInt32LittleEndian(tree.AsSpan(position));
			Int32 packedSize = BinaryPrimitives.ReadInt32LittleEndian(tree.AsSpan(position + 4));
			UInt32 offset = BinaryPrimitives.ReadUInt32LittleEndian(tree.AsSpan(position + 8));
			position += 12;

			// Some tools do not set the flag; a size difference means that the data is compressed.
			if (!compressed && unpackedSize != packedSize)
			{
				compressed = true;
			}

			entry = CreateEntry(path, compressed, unpackedSize, packedSize, offset);
			return true;
		}

		private static DatArchiveReader ReadFallout1(String filePath, FileStream stream, Int32 directoryCount, Int64 length)
		{
			DatArchiveReader archive = new DatArchiveReader(filePath, DatFormat.Fallout1, length);
			stream.Position = Fallout1HeaderSize;

			// Only the directory is read, sequentially: a buffer keeps the many small reads cheap. It is not disposed on purpose,
			// that would close the file stream the caller disposes.
			BufferedStream buffered = new BufferedStream(stream, 64 * 1024);
			List<String> directories = new(directoryCount);
			for (Int32 index = 0; index < directoryCount; index++)
			{
				String name = ReadString(buffered).Trim('\\');
				directories.Add(name == "." || name.Length == 0 ? String.Empty : name + '\\');
			}

			Byte[] buffer = new Byte[16];
			foreach (String directory in directories)
			{
				buffered.ReadExactly(buffer);
				Int32 fileCount = BinaryPrimitives.ReadInt32BigEndian(buffer);
				if (fileCount < 0)
				{
					throw new DatException(DatErrorKind.InvalidTree, "The directory contents of the DAT file are corrupted.", filePath);
				}

				for (Int32 index = 0; index < fileCount; index++)
				{
					String name = ReadString(buffered);
					buffered.ReadExactly(buffer);

					Boolean compressed = BinaryPrimitives.ReadInt32BigEndian(buffer) == 0x40;
					UInt32 offset = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4));
					Int32 unpackedSize = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(8));
					Int32 packedSize = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(12));

					archive._index.Add(CreateEntry(directory + name, compressed, unpackedSize, packedSize, offset));
				}
			}

			return archive;
		}

		private static String ReadString(Stream stream)
		{
			Int32 length = stream.ReadByte();
			if (length < 0)
			{
				throw new EndOfStreamException();
			}

			Byte[] bytes = new Byte[length];
			stream.ReadExactly(bytes);
			return NameEncoding.GetString(bytes);
		}

		private static DatEntry CreateEntry(String path, Boolean compressed, Int32 unpackedSize, Int32 packedSize, UInt32 offset)
		{
			DatPaths.Split(DatPaths.NormalizePath(path), out String directory, out String name);
			return new DatEntry(directory, name)
			{
				IsCompressed = compressed,
				UnpackedSize = unpackedSize,
				PackedSize = packedSize,
				Offset = offset,
			};
		}
	}
}