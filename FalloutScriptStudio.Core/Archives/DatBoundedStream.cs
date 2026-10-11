namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// A read-only window over a part of a file: the stored data of one archive entry. It owns the file stream.
	/// </summary>
	internal sealed class DatBoundedStream : Stream
	{
		private readonly FileStream _file;
		private readonly Int64 _start;
		private readonly Int64 _length;
		private Int64 _position;

		public DatBoundedStream(FileStream file, Int64 start, Int64 length)
		{
			this._file = file;
			this._start = start;
			this._length = length;
		}

		public override Boolean CanRead => true;

		public override Boolean CanSeek => true;

		public override Boolean CanWrite => false;

		public override Int64 Length => this._length;

		public override Int64 Position
		{
			get => this._position;
			set
			{
				ArgumentOutOfRangeException.ThrowIfNegative(value);
				this._position = value;
			}
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

			this._file.Position = this._start + this._position;
			Int32 read = this._file.Read(buffer.Slice(0, count));
			this._position += read;
			return read;
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

			this._file.Position = this._start + this._position;
			Int32 read = await this._file.ReadAsync(buffer.Slice(0, count), cancellationToken).ConfigureAwait(false);
			this._position += read;
			return read;
		}

		public override Int64 Seek(Int64 offset, SeekOrigin origin)
		{
			Int64 target = origin switch
			{
				SeekOrigin.Begin => offset,
				SeekOrigin.Current => this._position + offset,
				SeekOrigin.End => this._length + offset,
				_ => throw new ArgumentOutOfRangeException(nameof(origin)),
			};

			this.Position = target;
			return target;
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
				this._file.Dispose();
			}

			base.Dispose(disposing);
		}

		private Int32 Clamp(Int32 requested)
		{
			Int64 remaining = this._length - this._position;
			return remaining <= 0 ? 0 : (Int32)Math.Min(requested, remaining);
		}
	}
}
