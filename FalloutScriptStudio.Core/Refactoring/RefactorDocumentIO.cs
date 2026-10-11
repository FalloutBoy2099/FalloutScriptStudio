namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>
	/// Reads project files into <see cref="RefactorDocument"/> objects and writes the result of a refactoring back to files that are
	/// not open in an editor.
	/// </summary>
	public static class RefactorDocumentIO
	{
		/// <summary>
		/// Lazily reads files; use it as the <c>documents</c> argument of a preview so large projects are read one file at a time.
		/// </summary>
		/// <param name="paths">The files to read.</param>
		/// <param name="skip">Paths to leave out, for example the files that are open in an editor (their text comes from the editor); may be <c>null</c>.</param>
		/// <param name="encoding">The encoding of the files.</param>
		/// <returns>The documents; files that cannot be read are skipped.</returns>
		public static IEnumerable<RefactorDocument> ReadFiles(IEnumerable<String> paths, ISet<String>? skip, Encoding encoding)
		{
			ArgumentNullException.ThrowIfNull(paths);
			ArgumentNullException.ThrowIfNull(encoding);

			foreach (String path in paths)
			{
				if (skip is not null && skip.Contains(path))
				{
					continue;
				}

				String text;
				try
				{
					text = File.ReadAllText(path, encoding);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					continue;
				}

				yield return new RefactorDocument(path, text);
			}
		}

		/// <summary>
		/// Applies the edits of a refactoring to the files that are not open in an editor and writes them atomically.
		/// </summary>
		/// <param name="result">The result of the refactoring.</param>
		/// <param name="documents">The documents the edits were computed for (their text is the base of the edits).</param>
		/// <param name="encoding">The encoding to write with; the encoding the files were read with keeps them unchanged otherwise.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The paths of the files that were written. Edits of open documents are left to the editor.</returns>
		public static async Task<IReadOnlyList<String>> WriteClosedFilesAsync(RenameResult result, IReadOnlyList<RefactorDocument> documents, Encoding encoding, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(result);
			ArgumentNullException.ThrowIfNull(documents);
			ArgumentNullException.ThrowIfNull(encoding);

			var written = new List<String>();
			foreach (FileEdits file in result.Files.Where(f => !f.IsOpen))
			{
				RefactorDocument? document = documents.FirstOrDefault(d => String.Equals(d.Path, file.Path, StringComparison.OrdinalIgnoreCase));
				if (document is null)
				{
					continue;
				}

				await AtomicFile.WriteAllBytesAsync(file.Path, encoding.GetBytes(file.ApplyTo(document.Text)), cancellationToken).ConfigureAwait(false);
				written.Add(file.Path);
			}

			return written;
		}
	}
}
