namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;
	using Documents;

	/// <summary>
	/// A file that formatting would change: where it is, how many lines would change and a hash of the content that was looked at (so that
	/// the file is left alone when it changed since). The text itself is not kept, so a preview of 5 000 files stays small.
	/// </summary>
	public sealed class FormatFileChange
	{
		/// <summary>Initializes a new instance of the <see cref="FormatFileChange"/> class.</summary>
		/// <param name="path">The full path of the file, or the name of a document that was never saved.</param>
		/// <param name="displayName">The path as shown to the user (relative to the project folder when it is inside).</param>
		/// <param name="changedLines">The number of lines that would change.</param>
		/// <param name="hash">The hash of the content that was looked at.</param>
		/// <param name="document">The open document of the file, or <see langword="null"/> when the file is read from the disk.</param>
		public FormatFileChange(String path, String displayName, Int32 changedLines, String hash, DocumentViewModel? document)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(displayName);
			ArgumentNullException.ThrowIfNull(hash);
			this.Path = path;
			this.DisplayName = displayName;
			this.ChangedLines = changedLines;
			this.Hash = hash;
			this.Document = document;
		}

		/// <summary>Gets the full path of the file, or the name of a document that was never saved.</summary>
		public String Path { get; }

		/// <summary>Gets the path as shown to the user.</summary>
		public String DisplayName { get; }

		/// <summary>Gets the number of lines that would change.</summary>
		public Int32 ChangedLines { get; }

		/// <summary>Gets the hash of the content that was looked at.</summary>
		public String Hash { get; }

		/// <summary>Gets the open document of the file, or <see langword="null"/> when the file is read from the disk.</summary>
		public DocumentViewModel? Document { get; }

		/// <summary>Gets a value indicating whether the file is open in the editor (it is changed through its document, as one undo step).</summary>
		public Boolean IsOpen => this.Document is not null;
	}
}