namespace FalloutScriptStudio.Core.Archives
{
	using System;

	/// <summary>
	/// Raised when a DAT archive is invalid or one of its files cannot be read. The message is in English, for logs.
	/// </summary>
	public sealed class DatException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DatException"/> class.
		/// </summary>
		/// <param name="kind">What went wrong.</param>
		/// <param name="message">The description of the problem.</param>
		/// <param name="path">The archive or the file inside it the problem concerns, if any.</param>
		public DatException(DatErrorKind kind, String message, String? path = null)
			: base(message)
		{
			this.Kind = kind;
			this.Path = path;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DatException"/> class.
		/// </summary>
		/// <param name="kind">What went wrong.</param>
		/// <param name="message">The description of the problem.</param>
		/// <param name="innerException">The exception that caused this one.</param>
		/// <param name="path">The archive or the file inside it the problem concerns, if any.</param>
		public DatException(DatErrorKind kind, String message, Exception innerException, String? path = null)
			: base(message, innerException)
		{
			this.Kind = kind;
			this.Path = path;
		}

		/// <summary>Gets what went wrong.</summary>
		public DatErrorKind Kind { get; }

		/// <summary>Gets the archive or the file inside it the problem concerns, or <see langword="null"/>.</summary>
		public String? Path { get; }
	}
}
