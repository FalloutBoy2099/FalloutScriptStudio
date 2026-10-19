namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Indexing;
	using Parsing;

	/// <summary>
	/// Builds the <see cref="ScriptEnvironment"/> of a script: it follows the <c>#include</c> directives (the same way the compiler and the
	/// parser find the files, see <see cref="IncludeResolver"/>), reads the symbols of every header through the <see cref="IncludeSymbolCache"/>
	/// and marks the environment incomplete when a header cannot be found or read.
	/// </summary>
	public sealed class ScriptEnvironmentBuilder
	{
		/// <summary>The deepest chain of includes that is followed.</summary>
		public const Int32 MaximumDepth = 16;

		/// <summary>The most header files that are read for one script.</summary>
		public const Int32 MaximumFiles = 600;

		private readonly IncludeResolver _resolver;
		private readonly IncludeSymbolCache _cache;
		private readonly Func<String, String?> _readFile;
		private readonly Func<String, String?>? _overlay;
		private readonly Func<String, String?>? _locate;

		/// <summary>Initializes a new instance of the <see cref="ScriptEnvironmentBuilder"/> class.</summary>
		/// <param name="resolver">Finds the file of an include directive.</param>
		/// <param name="cache">Remembers the symbols of the headers.</param>
		/// <param name="readFile">Reads the text of a header (with the code page of the editor); returns <see langword="null"/> when it cannot be read.</param>
		/// <param name="overlay">Gives the text of a file that is open in the editor with changes that are not saved, or <see langword="null"/>.</param>
		/// <param name="locate">Finds the file of an include the resolver did not find (for example by name in the index of the project), or <see langword="null"/>.</param>
		public ScriptEnvironmentBuilder(IncludeResolver resolver, IncludeSymbolCache cache, Func<String, String?> readFile, Func<String, String?>? overlay = null, Func<String, String?>? locate = null)
		{
			ArgumentNullException.ThrowIfNull(resolver);
			ArgumentNullException.ThrowIfNull(cache);
			ArgumentNullException.ThrowIfNull(readFile);
			this._resolver = resolver;
			this._cache = cache;
			this._readFile = readFile;
			this._overlay = overlay;
			this._locate = locate;
		}

		/// <summary>Reads a header the way the editor opens a file: a byte order mark wins, valid UTF-8 is UTF-8, everything else uses the given code page.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <param name="fallback">The code page of the scripts.</param>
		/// <returns>The text, or <see langword="null"/> when the file cannot be read or is a binary file.</returns>
		public static String? ReadText(String path, Encoding fallback)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(fallback);

			try
			{
				Byte[] bytes = SharedFile.ReadAllBytes(path);
				return IndexTextDecoder.LooksBinary(bytes) ? null : IndexTextDecoder.Decode(bytes, fallback);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return null;
			}
		}

		/// <summary>Builds the environment of a script.</summary>
		/// <param name="tokens">The tokens of the script.</param>
		/// <param name="scriptPath">The full path of the script, or <see langword="null"/> when it was never saved.</param>
		/// <param name="findMacro">Finds the macro of a name in the headers (<c>ProgramInfo.FindMacro</c>), or <see langword="null"/>.</param>
		/// <param name="functions">The descriptions of the functions, or <see langword="null"/>.</param>
		/// <returns>The environment.</returns>
		public ScriptEnvironment Build(SslTokenStream tokens, String? scriptPath, Func<String, ScriptMacro?>? findMacro, FunctionSignatureCatalog? functions)
		{
			ArgumentNullException.ThrowIfNull(tokens);

			HashSet<String> visited = new(StringComparer.OrdinalIgnoreCase);
			if (!String.IsNullOrEmpty(scriptPath))
			{
				visited.Add(NormalizePath(scriptPath));
			}

			List<ScriptSymbolTable> tables = [];
			String? directory = String.IsNullOrEmpty(scriptPath) ? null : Path.GetDirectoryName(scriptPath);
			Boolean complete = this.Follow(ScriptDiagnostics.FindIncludes(tokens), directory, 0, visited, tables);
			return new ScriptEnvironment
			{
				FindMacro = findMacro,
				Symbols = ExternalSymbols.Create(tables),
				IsComplete = complete,
				Functions = functions ?? FunctionSignatureCatalog.Empty,
			};
		}

		private static String NormalizePath(String path)
		{
			try
			{
				return Path.GetFullPath(path);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path;
			}
		}

		/// <summary>Reads the includes of one file. Returns <see langword="false"/> when something could not be read.</summary>
		private Boolean Follow(IReadOnlyList<String> includes, String? directory, Int32 depth, HashSet<String> visited, List<ScriptSymbolTable> tables)
		{
			Boolean complete = true;
			foreach (String include in includes)
			{
				String path = this._resolver.Resolve(include, directory);
				if (!File.Exists(path) && (this._overlay?.Invoke(path) is null))
				{
					String? located = this._locate?.Invoke(include);
					if (located is null || !File.Exists(located))
					{
						complete = false;
						continue;
					}

					path = located;
				}

				path = NormalizePath(path);
				if (!visited.Add(path))
				{
					continue;
				}

				if (visited.Count > MaximumFiles || depth >= MaximumDepth)
				{
					complete = false;
					continue;
				}

				String? unsaved = this._overlay?.Invoke(path);
				ScriptSymbolTable? table = unsaved is not null ? ScriptSymbolTable.Extract(unsaved) : this._cache.GetOrRead(path, this._readFile);
				if (table is null)
				{
					complete = false;
					continue;
				}

				tables.Add(table);
				if (table.IsOpaque)
				{
					complete = false;
				}

				if (table.Includes.Count > 0 && !this.Follow(table.Includes, Path.GetDirectoryName(path), depth + 1, visited, tables))
				{
					complete = false;
				}
			}

			return complete;
		}
	}
}