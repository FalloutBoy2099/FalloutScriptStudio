namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Decompresses the LZSS data of a Fallout 1 archive entry while it is read.
	/// See https://falloutmods.fandom.com/wiki/DAT_file_format#Fallout_1_LZSS_uncompression_algorithm
	/// </summary>
	/// <remarks>
	/// The data is a sequence of blocks, each starting with a signed 16 bit big endian size: a negative size announces that many
	/// raw bytes, zero ends the data, a positive size announces that many bytes of flags, literals and (offset, length) pairs
	/// that refer to a 4096 byte dictionary. The stream ends early (without an error of its own) when the source ends; the
	/// caller compares the number of bytes it received with the size it expects.
	/// </remarks>
	internal sealed class LzssDecompressStream : Stream
	{
		private const Int32 DictionarySize = 4096;
		private const Int32 DictionaryMask = DictionarySize - 1;
		private const Int32 MinMatch = 3;
		private const Int32 MaxMatch = 18;
		private const Byte DictionaryFill = 0x20;

		private readonly Stream _source;
		private readonly Byte[] _input = new Byte[16 * 1024];
		private readonly Byte[] _dictionary = new Byte[DictionarySize];

		private Int32 _inputPosition;
		private Int32 _inputLength;
		private Int32 _dictionaryIndex;
		private Int32 _blockRemaining;
		private Int32 _rawRemaining;
		private Int32 _flags;
		private Int32 _flagBits;
		private Int32 _matchOffset;
		private Int32 _matchRemaining;
		private Boolean _finished;

		public LzssDecompressStream(Stream source)
		{
			this._source = source;
		}

		public override Boolean CanRead => true;

		public override Boolean CanSeek => false;

		public override Boolean CanWrite => false;

		public override Int64 Length => throw new NotSupportedException();

		public override Int64 Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
		{
			return this.Read(buffer.AsSpan(offset, count));
		}

		public override Int32 Read(Span<Byte> buffer)
		{
			Int32 written = 0;
			while (written < buffer.Length && !this._finished)
			{
				if (this._matchRemaining > 0)
				{
					Byte value = this._dictionary[this._matchOffset & DictionaryMask];
					this._matchOffset++;
					this._matchRemaining--;
					buffer[written++] = value;
					this.AddToDictionary(value);
				}
				else if (this._rawRemaining > 0)
				{
					Int32 value = this.ReadInput();
					if (value < 0)
					{
						this._finished = true;
						break;
					}

					this._rawRemaining--;
					buffer[written++] = (Byte)value;
				}
				else if (this._blockRemaining > 0)
				{
					this.StepCompressedBlock(buffer, ref written);
				}
				else
				{
					this.StartBlock();
				}
			}

			return written;
		}

		public override Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
		{
			return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
		}

		public override ValueTask<Int32> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default)
		{
			// The decoder is synchronous (it pulls single bytes); decoding a buffer is quick, so it runs on the thread pool.
			return new ValueTask<Int32>(Task.Run(() => this.Read(buffer.Span), cancellationToken));
		}

		public override Int64 Seek(Int64 offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void Flush()
		{
		}

		public override void SetLength(Int64 value)
		{
			throw new NotSupportedException();
		}

		public override void Write(Byte[] buffer, Int32 offset, Int32 count)
		{
			throw new NotSupportedException();
		}

		protected override void Dispose(Boolean disposing)
		{
			if (disposing)
			{
				this._source.Dispose();
			}

			base.Dispose(disposing);
		}

		/// <summary>Reads the size of the next block and prepares to decode it.</summary>
		private void StartBlock()
		{
			Int32 high = this.ReadInput();
			Int32 low = high < 0 ? -1 : this.ReadInput();
			if (low < 0)
			{
				this._finished = true;
				return;
			}

			Int32 blockSize = (Int16)((high << 8) | low);
			if (blockSize == 0)
			{
				this._finished = true;
			}
			else if (blockSize < 0)
			{
				this._rawRemaining = -blockSize;
			}
			else
			{
				this._blockRemaining = blockSize;
				this._flagBits = 0;
				this._dictionaryIndex = DictionarySize - MaxMatch;
				Array.Fill(this._dictionary, DictionaryFill);
			}
		}

		/// <summary>Decodes one step of a compressed block: a flags byte, a literal byte or the start of a match.</summary>
		private void StepCompressedBlock(Span<Byte> buffer, ref Int32 written)
		{
			if (this._flagBits == 0)
			{
				Int32 flags = this.ReadInput();
				if (flags < 0)
				{
					this._finished = true;
					return;
				}

				this._flags = flags;
				this._flagBits = 8;
				this._blockRemaining--;
				return;
			}

			Boolean literal = (this._flags & 1) == 1;
			this._flags >>= 1;
			this._flagBits--;

			if (literal)
			{
				Int32 value = this.ReadInput();
				if (value < 0)
				{
					this._finished = true;
					return;
				}

				this._blockRemaining--;
				buffer[written++] = (Byte)value;
				this.AddToDictionary((Byte)value);
				return;
			}

			if (this._blockRemaining < 2)
			{
				// A match is cut by the end of the block: the rest of the block is dropped.
				this.SkipInput(this._blockRemaining);
				this._blockRemaining = 0;
				return;
			}

			Int32 low = this.ReadInput();
			Int32 high = low < 0 ? -1 : this.ReadInput();
			if (high < 0)
			{
				this._finished = true;
				return;
			}

			this._blockRemaining -= 2;
			this._matchOffset = low | ((high & 0xF0) << 4);
			this._matchRemaining = (high & 0x0F) + MinMatch;
		}

		private void AddToDictionary(Byte value)
		{
			this._dictionary[this._dictionaryIndex] = value;
			this._dictionaryIndex = (this._dictionaryIndex + 1) & DictionaryMask;
		}

		private void SkipInput(Int32 count)
		{
			for (Int32 index = 0; index < count && this.ReadInput() >= 0; index++)
			{
			}
		}

		private Int32 ReadInput()
		{
			if (this._inputPosition >= this._inputLength)
			{
				this._inputLength = this._source.Read(this._input, 0, this._input.Length);
				this._inputPosition = 0;
				if (this._inputLength <= 0)
				{
					this._inputLength = 0;
					return -1;
				}
			}

			return this._input[this._inputPosition++];
		}
	}
}