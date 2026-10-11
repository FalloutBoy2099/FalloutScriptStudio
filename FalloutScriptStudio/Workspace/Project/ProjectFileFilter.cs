namespace FalloutScriptStudio.Workspace.Project
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;

	/// <summary>Decides which files and folders the project explorer lists and finds files by name.</summary>
	public sealed class ProjectFileFilter
	{
		private static readonly String[] _sourceExtensions = { ".ssl", ".h", ".msg", ".fcd" };
		private static readonly String[] _compiledExtensions = { ".int" };

		/// <summary>The most files a filtered search returns.</summary>
		public const Int32 MaxSearchResults = 500;

		/// <summary>Initializes the filter.</summary>
		/// <param name="includeCompiled">True to list compiled scripts (<c>.int</c>) as well.</param>
		public ProjectFileFilter(Boolean includeCompiled)
		{
			this.IncludeCompiled = includeCompiled;
		}

		/// <summary>Gets a value indicating whether compiled scripts are listed.</summary>
		public Boolean IncludeCompiled { get; }

		/// <summary>Determines whether a file is listed.</summary>
		public Boolean IsListed(String path)
		{
			String extension = Path.GetExtension(path);
			return _sourceExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
				|| (this.IncludeCompiled && _compiledExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
		}

		/// <summary>Determines whether a folder is listed (hidden folders such as <c>.git</c> are not).</summary>
		public static Boolean IsListedDirectory(String path)
		{
			String name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			return !name.StartsWith('.');
		}

		/// <summary>Lists the sub folders of a folder, sorted by name; unreadable folders yield an empty list.</summary>
		public IReadOnlyList<String> GetDirectories(String directory)
		{
			try
			{
				return Directory.EnumerateDirectories(directory)
					.Where(IsListedDirectory)
					.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Array.Empty<String>();
			}
		}

		/// <summary>Lists the listed files of a folder, sorted by name; unreadable folders yield an empty list.</summary>
		public IReadOnlyList<String> GetFiles(String directory)
		{
			try
			{
				return Directory.EnumerateFiles(directory)
					.Where(this.IsListed)
					.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Array.Empty<String>();
			}
		}

		/// <summary>Finds listed files whose name contains the text, anywhere below a folder.</summary>
		/// <param name="root">The folder to start from.</param>
		/// <param name="text">The text a file name must contain (case is ignored).</param>
		/// <param name="cancellationToken">Cancels the walk.</param>
		/// <returns>At most <see cref="MaxSearchResults"/> paths.</returns>
		/// <exception cref="OperationCanceledException">The walk was cancelled.</exception>
		public IReadOnlyList<String> Search(String root, String text, CancellationToken cancellationToken = default)
		{
			return FilterByName(this.EnumerateTree(root, cancellationToken), text);
		}

		/// <summary>Lists every listed file below a folder, depth first, folder by folder in name order.</summary>
		/// <param name="root">The folder to start from.</param>
		/// <param name="cancellationToken">Cancels the walk.</param>
		/// <returns>The full paths.</returns>
		/// <exception cref="OperationCanceledException">The walk was cancelled.</exception>
		public IReadOnlyList<String> ListAll(String root, CancellationToken cancellationToken = default)
		{
			return this.EnumerateTree(root, cancellationToken).ToList();
		}

		/// <summary>Keeps the files whose name contains a text.</summary>
		/// <param name="files">The paths, for example the result of <see cref="ListAll"/> (they are enumerated lazily).</param>
		/// <param name="text">The text a file name must contain (case is ignored).</param>
		/// <returns>At most <see cref="MaxSearchResults"/> paths, in the order of <paramref name="files"/>.</returns>
		public static IReadOnlyList<String> FilterByName(IEnumerable<String> files, String text)
		{
			List<String> found = new List<String>();
			foreach (String file in files)
			{
				if (Path.GetFileName(file).Contains(text, StringComparison.OrdinalIgnoreCase))
				{
					found.Add(file);
					if (found.Count >= MaxSearchResults)
					{
						break;
					}
				}
			}

			return found;
		}

		private IEnumerable<String> EnumerateTree(String root, CancellationToken cancellationToken)
		{
			Stack<String> pending = new Stack<String>();
			pending.Push(root);
			while (pending.Count > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				String directory = pending.Pop();
				foreach (String file in this.GetFiles(directory))
				{
					yield return file;
				}

				foreach (String sub in this.GetDirectories(directory).Reverse())
				{
					pending.Push(sub);
				}
			}
		}
	}
}
