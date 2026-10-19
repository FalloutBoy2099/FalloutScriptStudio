namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	/// <summary>
	/// Reads a folder as if it were an archive: the files of the folder and of its sub folders are the entries, with paths relative
	/// to the folder. The Fallout engines accept a folder named like a <c>.dat</c> file in place of the archive.
	/// </summary>
	public sealed class DatFolderReader : IDatArchiveReader
	{
		private readonly String _root;
		private readonly Lazy<DatEntryIndex> _index;

		private DatFolderReader(String root)
		{
			this._root = root;
			this._index = new Lazy<DatEntryIndex>(this.Enumerate);
		}

		/// <inheritdoc/>
		public String FilePath => this._root;

		/// <inheritdoc/>
		public DatFormat Format => DatFormat.Folder;

		/// <summary>Gets all the files of the folder; the folder is scanned the first time this is used.</summary>
		public IReadOnlyList<DatEntry> Entries => this._index.Value.Entries;

		/// <summary>
		/// Opens a folder. Nothing is scanned until the list of entries is needed: <see cref="Find"/> asks the file system directly.
		/// </summary>
		/// <param name="folderPath">The folder.</param>
		/// <returns>The reader.</returns>
		/// <exception cref="DatException">The folder does not exist.</exception>
		public static DatFolderReader Open(String folderPath)
		{
			if (String.IsNullOrWhiteSpace(folderPath))
			{
				throw new DatException(DatErrorKind.InvalidFileName, "The folder name is empty.");
			}

			String full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
			if (!Directory.Exists(full))
			{
				throw new DatException(DatErrorKind.FileNotFound, "The folder was not found: " + full, full);
			}

			return new DatFolderReader(full);
		}

		/// <inheritdoc/>
		public DatEntry? Find(String path)
		{
			String normalized = DatPaths.NormalizePath(path);
			if (normalized.Length == 0 || normalized.Contains(':', StringComparison.Ordinal) || HasParentSegment(normalized))
			{
				return null;
			}

			String file = Path.Combine(this._root, normalized.Replace('\\', Path.DirectorySeparatorChar));
			try
			{
				FileInfo info = new FileInfo(file);
				return info.Exists && info.Length <= Int32.MaxValue ? this.CreateEntry(info) : null;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
			{
				return null;
			}
		}

		/// <inheritdoc/>
		public IReadOnlyList<DatEntry> GetEntries(String directory)
		{
			return this._index.Value.GetEntries(directory);
		}

		/// <inheritdoc/>
		public Stream OpenEntry(DatEntry entry)
		{
			ArgumentNullException.ThrowIfNull(entry);
			if (entry.SourceFile is null)
			{
				throw new ArgumentException("The entry does not belong to a folder.", nameof(entry));
			}

			try
			{
				return new FileStream(entry.SourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				throw new DatException(DatErrorKind.Io, exception.Message, exception, entry.FullPath);
			}
		}

		private static Boolean HasParentSegment(String path)
		{
			foreach (String segment in path.Split('\\'))
			{
				if (segment == "..")
				{
					return true;
				}
			}

			return false;
		}

		private DatEntryIndex Enumerate()
		{
			DatEntryIndex index = new DatEntryIndex();
			EnumerationOptions options = new EnumerationOptions
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.ReparsePoint,
			};

			DirectoryInfo root = new DirectoryInfo(this._root);
			foreach (FileInfo info in root.EnumerateFiles("*", options))
			{
				// A file larger than 2 GB cannot be described by an entry, and no script is that large.
				if (info.Length <= Int32.MaxValue)
				{
					index.Add(this.CreateEntry(info));
				}
			}

			return index;
		}

		private DatEntry CreateEntry(FileInfo info)
		{
			String relative = Path.GetRelativePath(this._root, info.FullName).Replace(Path.DirectorySeparatorChar, '\\');
			DatPaths.Split(relative, out String directory, out String name);
			return new DatEntry(directory, name)
			{
				UnpackedSize = (Int32)info.Length,
				PackedSize = (Int32)info.Length,
				SourceFile = info.FullName,
			};
		}
	}
}