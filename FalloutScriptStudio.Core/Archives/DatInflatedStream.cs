namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// The decompressed content of an archive entry: it never produces more than the announced size, fails when the compressed
	/// data ends early, and reports damaged data as a <see cref="DatException"/>. It owns the decompressing stream.
	/// </summary>
	internal sealed class DatInflatedStream : Stream
	{
		private readonly Stream _inner;
		private readonly Int64 _expectedLength;
		private readonly String _path;
		private Int64 _position;

		public DatInflatedStream(Stream inner, Int64 expectedLength, String path)
		{
			this._inner = inner;
			this._expectedLength = expectedLength;
			this._path = path;
		}

		public override Boolean CanRead => true;

		public override Boolean CanSeek => false;

		public override Boolean CanWrite => false;

		public override Int64 Length => throw new NotSupportedException();

		public override Int64 Position
		{
			get => this._position;
			set => throw new NotSupportedException();
		}

		public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
		{
			return this.Read(buffer.AsSpan(offset, count));
		}

		public override Int32 Read(Span<Byte> buffer)
		{
			Int32 count = this.Clamp(buffer.Length);
			if (count <= 0)
			{
				return 0;
			}

			Int32 read;
			try
			{
				read = this._inner.Read(buffer.Slice(0, count));
			}
			catch (Exception exception) when (IsCorruption(exception))
			{
				throw this.Corrupt(exception);
			}

			return this.Account(read);
		}

		public override Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
		{
			return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
		}

		public override async ValueTask<Int32> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default)
		{
			Int32 count = this.Clamp(buffer.Length);
			if (count <= 0)
			{
				return 0;
			}

			Int32 read;
			try
			{
				read = await this._inner.ReadAsync(buffer.Slice(0, count), cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (IsCorruption(exception))
			{
				throw this.Corrupt(exception);
			}

			return this.Account(read);
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
				this._inner.Dispose();
			}

			base.Dispose(disposing);
		}

		private static Boolean IsCorruption(Exception exception)
		{
			return exception is InvalidDataException or EndOfStreamException;
		}

		private Int32 Clamp(Int32 requested)
		{
			Int64 remaining = this._expectedLength - this._position;
			return remaining <= 0 ? 0 : (Int32)Math.Min(requested, remaining);
		}

		private Int32 Account(Int32 read)
		{
			if (read == 0)
			{
				throw this.Corrupt(new InvalidDataException("The compressed data of the file is truncated."));
			}

			this._position += read;
			return read;
		}

		private DatException Corrupt(Exception exception)
		{
			return new DatException(DatErrorKind.CorruptData, "The data of '" + this._path + "' is corrupted.", exception, this._path);
		}
	}
}