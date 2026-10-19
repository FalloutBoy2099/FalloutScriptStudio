namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using IO;

	/// <summary>
	/// Immutable options that control where message files are looked for and how they are encoded.
	/// </summary>
	public sealed record MessageFileOptions
	{
		/// <summary>Gets the folder with the compiled scripts and <c>scripts.lst</c> (the game's <c>scripts</c> folder), or <see langword="null"/> if not set.</summary>
		public String? OutputDirectory { get; init; }

		/// <summary>Gets the game language, the name of the folder below <c>text</c>.</summary>
		public String Language { get; init; } = "english";

		/// <summary>Gets a value indicating whether message files are associated through the script id in <c>scripts.lst</c> instead of the script name.</summary>
		public Boolean AssociateByScriptId { get; init; }

		/// <summary>Gets additional folders searched for message files (each folder and its direct sub folders).</summary>
		public IReadOnlyList<String> SearchDirectories { get; init; } = Array.Empty<String>();

		/// <summary>Gets the encoding of message files.</summary>
		public Encoding Encoding { get; init; } = TextEncodings.Ansi;

		/// <summary>Gets the folder with the dialog message files of the current language (<c>..\text\&lt;language&gt;\dialog</c>), or <see langword="null"/> if <see cref="OutputDirectory"/> is not set.</summary>
		public String? DialogDirectory => this.OutputDirectory == null
			? null
			: Path.GetFullPath(Path.Combine(this.OutputDirectory, "..", "text", this.Language, "dialog"));
	}
}