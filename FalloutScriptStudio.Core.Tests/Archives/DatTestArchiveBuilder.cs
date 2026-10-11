namespace FalloutScriptStudio.Core.Tests.Archives
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Text;

	/// <summary>
	/// Writes small Fallout 1 and Fallout 2 <c>.dat</c> archives for the tests. Independent of the reader under test.
	/// </summary>
	internal sealed class DatTestArchiveBuilder
	{
		private readonly List<TestFile> _files = new List<TestFile>();

		/// <summary>Adds a file stored without compression.</summary>
		public DatTestArchiveBuilder AddStored(String path, Byte[] content)
		{
			this._files.Add(new TestFile(path, content, content, false));
			return this;
		}

		/// <summary>Adds a file compressed with zlib (Fallout 2) or LZSS (Fallout 1, see <see cref="CompressLzss"/>).</summary>
		public DatTestArchiveBuilder AddCompressed(String path, Byte[] content)
		{
			this._files.Add(new TestFile(path, content, null, true));
			return this;
		}

		/// <summary>Adds a file with the exact bytes the archive should store, flagged as compressed.</summary>
		public DatTestArchiveBuilder AddRawCompressed(String path, Byte[] unpacked, Byte[] stored)
		{
			this._files.Add(new TestFile(path, unpacked, stored, true));
			return this;
		}

		/// <summary>Compresses with zlib.</summary>
		public static Byte[] CompressZlib(Byte[] data)
		{
			using MemoryStream result = new MemoryStream();
			using (ZLibStream zlib = new ZLibStream(result, CompressionLevel.Optimal, true))
			{
				zlib.Write(data);
			}

			return result.ToArray();
		}

		/// <summary>
		/// A small LZSS encoder for Fallout 1: it finds matches in the dictionary the decoder has (4096 bytes, filled with spaces
		/// and addressed by absolute position), in blocks of at most 4000 input bytes.
		/// </summary>
		public static Byte[] CompressLzss(Byte[] data)
		{
			const Int32 DictionarySize = 4096;
			const Int32 BlockInput = 4000;

			using MemoryStream output = new MemoryStream();
			Int32 position = 0;
			while (position < data.Length)
			{
				Int32 blockEnd = Math.Min(data.Length, position + BlockInput);
				Byte[] dictionary = new Byte[DictionarySize];
				Array.Fill(dictionary, (Byte)0x20);
				Int32 dictionaryIndex = DictionarySize - 18;

				using MemoryStream block = new MemoryStream();
				while (position < blockEnd)
				{
					Int32 flagsPosition = (Int32)block.Position;
					block.WriteByte(0);
					Int32 flags = 0;
					for (Int32 bit = 0; bit < 8 && position < blockEnd; bit++)
					{
						FindMatch(data, position, blockEnd, dictionary, dictionaryIndex, out Int32 bestOffset, out Int32 bestLength);
						Int32 consumed;
						if (bestLength >= 3)
						{
							block.WriteByte((Byte)(bestOffset & 0xFF));
							block.WriteByte((Byte)(((bestOffset >> 4) & 0xF0) | (bestLength - 3)));
							consumed = bestLength;
						}
						else
						{
							flags |= 1 << bit;
							block.WriteByte(data[position]);
							consumed = 1;
						}

						for (Int32 index = 0; index < consumed; index++)
						{
							dictionary[dictionaryIndex] = data[position + index];
							dictionaryIndex = (dictionaryIndex + 1) % DictionarySize;
						}

						position += consumed;
					}

					Byte[] current = block.ToArray();
					current[flagsPosition] = (Byte)flags;
					block.SetLength(0);
					block.Write(current);
				}

				Byte[] blockBytes = block.ToArray();
				output.WriteByte((Byte)(blockBytes.Length >> 8));
				output.WriteByte((Byte)blockBytes.Length);
				output.Write(blockBytes);
			}

			output.WriteByte(0);
			output.WriteByte(0);
			return output.ToArray();
		}

		/// <summary>A raw LZSS block (negative size) followed by the end marker.</summary>
		public static Byte[] LzssRawBlock(Byte[] data)
		{
			using MemoryStream output = new MemoryStream();
			Int16 size = (Int16)(-data.Length);
			output.WriteByte((Byte)(size >> 8));
			output.WriteByte((Byte)size);
			output.Write(data);
			output.WriteByte(0);
			output.WriteByte(0);
			return output.ToArray();
		}

		/// <summary>Builds a Fallout 2 archive: data, file count and entries, tree size, archive size.</summary>
		public Byte[] BuildFallout2()
		{
			using MemoryStream file = new MemoryStream();
			using MemoryStream tree = new MemoryStream();
			WriteInt32(tree, this._files.Count, false);
			foreach (TestFile entry in this._files)
			{
				Byte[] stored = entry.Stored ?? CompressZlib(entry.Content);
				UInt32 offset = (UInt32)file.Position;
				file.Write(stored);

				Byte[] name = Encoding.Latin1.GetBytes(entry.Path.Replace('/', '\\'));
				WriteInt32(tree, name.Length, false);
				tree.Write(name);
				tree.WriteByte((Byte)(entry.Compressed ? 1 : 0));
				WriteInt32(tree, entry.Content.Length, false);
				WriteInt32(tree, stored.Length, false);
				WriteInt32(tree, (Int32)offset, false);
			}

			file.Write(tree.ToArray());
			WriteInt32(file, (Int32)tree.Length, false);
			WriteInt32(file, (Int32)(file.Length + 4), false);
			return file.ToArray();
		}

		/// <summary>Builds a Fallout 1 archive: header, directory names, directory contents, data. Numbers are big endian.</summary>
		public Byte[] BuildFallout1()
		{
			SortedDictionary<String, List<TestFile>> directories = new SortedDictionary<String, List<TestFile>>(StringComparer.OrdinalIgnoreCase);
			foreach (TestFile entry in this._files)
			{
				String path = entry.Path.Replace('/', '\\');
				Int32 separator = path.LastIndexOf('\\');
				String directory = separator < 0 ? "." : path.Substring(0, separator);
				if (!directories.TryGetValue(directory, out List<TestFile>? list))
				{
					list = new List<TestFile>();
					directories.Add(directory, list);
				}

				list.Add(entry with { Path = path.Substring(separator + 1) });
			}

			Int32 headerLength = 16;
			foreach (String directory in directories.Keys)
			{
				headerLength += 1 + directory.Length;
			}

			foreach (List<TestFile> list in directories.Values)
			{
				headerLength += 16;
				foreach (TestFile entry in list)
				{
					headerLength += 1 + entry.Path.Length + 16;
				}
			}

			using MemoryStream header = new MemoryStream();
			using MemoryStream data = new MemoryStream();
			WriteInt32(header, directories.Count, true);
			WriteInt32(header, 1, true);
			WriteInt32(header, 0, true);
			WriteInt32(header, 0, true);

			foreach (String directory in directories.Keys)
			{
				header.WriteByte((Byte)directory.Length);
				header.Write(Encoding.Latin1.GetBytes(directory));
			}

			foreach (List<TestFile> list in directories.Values)
			{
				WriteInt32(header, list.Count, true);
				WriteInt32(header, 16, true);
				WriteInt32(header, 16, true);
				WriteInt32(header, 0, true);
				foreach (TestFile entry in list)
				{
					Byte[] stored = entry.Stored ?? CompressLzss(entry.Content);
					header.WriteByte((Byte)entry.Path.Length);
					header.Write(Encoding.Latin1.GetBytes(entry.Path));
					WriteInt32(header, entry.Compressed ? 0x40 : 0x20, true);
					WriteInt32(header, headerLength + (Int32)data.Length, true);
					WriteInt32(header, entry.Content.Length, true);
					WriteInt32(header, stored.Length, true);
					data.Write(stored);
				}
			}

			header.Write(data.ToArray());
			return header.ToArray();
		}

		private static void FindMatch(Byte[] data, Int32 position, Int32 blockEnd, Byte[] dictionary, Int32 dictionaryIndex, out Int32 bestOffset, out Int32 bestLength)
		{
			const Int32 DictionarySize = 4096;
			bestOffset = 0;
			bestLength = 0;
			Int32 maxLength = Math.Min(18, blockEnd - position);
			for (Int32 offset = 0; offset < DictionarySize; offset++)
			{
				Int32 length = 0;
				while (length < maxLength)
				{
					Int32 source = (offset + length) % DictionarySize;
					Int32 written = (source - dictionaryIndex + DictionarySize) % DictionarySize;
					Byte value = written < length ? data[position + written] : dictionary[source];
					if (value != data[position + length])
					{
						break;
					}

					length++;
				}

				if (length > bestLength)
				{
					bestLength = length;
					bestOffset = offset;
				}
			}
		}

		private static void WriteInt32(Stream stream, Int32 value, Boolean bigEndian)
		{
			Byte[] bytes = new Byte[4];
			if (bigEndian)
			{
				BinaryPrimitives.WriteInt32BigEndian(bytes, value);
			}
			else
			{
				BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
			}

			stream.Write(bytes);
		}

		private sealed record TestFile(String Path, Byte[] Content, Byte[]? Stored, Boolean Compressed);
	}
}
