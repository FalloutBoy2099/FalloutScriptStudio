namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>
	/// The result of looking for the message file of a script.
	/// </summary>
	/// <param name="Found">Whether an existing file was found.</param>
	/// <param name="Path">The path of the existing file, or <see langword="null"/> if none was found.</param>
	/// <param name="FileName">The name of the message file that is looked for, or <see langword="null"/> if it could not be determined.</param>
	/// <param name="DefaultDirectory">The folder where a new message file would be created, or <see langword="null"/> if the name could not be determined.</param>
	/// <param name="Issues">Conditions the user interface may want to report.</param>
	public sealed record MessageFileLocation(Boolean Found, String? Path, String? FileName, String? DefaultDirectory, MessageLookupIssues Issues)
	{
		/// <summary>Gets the path where the message file would be created when it does not exist yet.</summary>
		public String? DefaultPath => this.FileName == null || this.DefaultDirectory == null
			? null
			: System.IO.Path.Combine(this.DefaultDirectory, this.FileName);
	}
}