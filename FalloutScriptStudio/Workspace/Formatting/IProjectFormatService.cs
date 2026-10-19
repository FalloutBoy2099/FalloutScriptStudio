namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Formats many files at once ("Format Project"): a preview tells which files would change, then the files the user chose are formatted
	/// with the safe rules of the other batch edits (a file that changed since the preview is left alone, the write is atomic and keeps the
	/// encoding and the line breaks, a read-only file is skipped, an open document is changed through the document as one undo step).
	/// </summary>
	public interface IProjectFormatService
	{
		/// <summary>Looks at the files of the scope and lists the ones that formatting would change. Call it on the UI thread.</summary>
		/// <param name="request">What to look at.</param>
		/// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
		/// <param name="cancellationToken">Stops the work (within a few files).</param>
		/// <returns>The files that would change.</returns>
		Task<ProjectFormatPreview> PreviewAsync(ProjectFormatRequest request, IProgress<FormatProgress>? progress, CancellationToken cancellationToken);

		/// <summary>Creates the unified diff of a file: what formatting would change in it now.</summary>
		/// <param name="change">The file.</param>
		/// <param name="cancellationToken">Stops the work.</param>
		/// <returns>The diff; empty when the file needs no change any more.</returns>
		Task<String> GetDiffAsync(FormatFileChange change, CancellationToken cancellationToken);

		/// <summary>Formats the given files. Call it on the UI thread.</summary>
		/// <param name="changes">The files to format (from <see cref="PreviewAsync"/>).</param>
		/// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
		/// <param name="cancellationToken">Stops after the file in progress; the files already done stay formatted.</param>
		/// <returns>How many files were formatted and how many were left alone.</returns>
		Task<ProjectFormatResult> ApplyAsync(IReadOnlyList<FormatFileChange> changes, IProgress<FormatProgress>? progress, CancellationToken cancellationToken);
	}
}