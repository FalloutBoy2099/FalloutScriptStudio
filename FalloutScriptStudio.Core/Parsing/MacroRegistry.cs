namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;

	/// <summary>
	/// The global macros shared by all scripts: every <c>#define</c> found in the game headers directory
	/// (replaces the legacy static <c>ProgramInfo.macrosGlobal</c>).
	/// </summary>
	/// <remarks>
	/// Register one instance as a singleton. Readers always see a complete, immutable snapshot, so the registry may be
	/// read from any thread while it is reloaded on another one.
	/// </remarks>
	public sealed class MacroRegistry
	{
		private static readonly IReadOnlyDictionary<String, ScriptMacro> _empty =
			new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);

		/// <summary>A header file modified more recently than this is always read again: its time stamp may not tell two saves apart yet.</summary>
		private static readonly TimeSpan _recentlyModified = TimeSpan.FromSeconds(2);

		private readonly Object _loadGate = new Object();
		private IReadOnlyDictionary<String, ScriptMacro> _macros = _empty;
		private String? _sourceDirectory;
		private HeaderStamp[]? _loadedStamps;

		/// <summary>Gets an immutable snapshot of the macros keyed by token (case-sensitive).</summary>
		public IReadOnlyDictionary<String, ScriptMacro> Macros => Volatile.Read(ref this._macros);

		/// <summary>Gets the number of macros.</summary>
		public Int32 Count => this.Macros.Count;

		/// <summary>Gets the directory the macros were loaded from, or <c>null</c> when nothing was loaded from a directory.</summary>
		public String? SourceDirectory => Volatile.Read(ref this._sourceDirectory);

		/// <summary>Determines whether a macro with the given (case-sensitive) token exists.</summary>
		/// <param name="token">The macro name.</param>
		/// <returns><c>true</c> when the macro exists.</returns>
		public Boolean Contains(String token)
		{
			return this.Macros.ContainsKey(token);
		}

		/// <summary>Looks up a macro by its (case-sensitive) token.</summary>
		/// <param name="token">The macro name.</param>
		/// <param name="macro">The macro, when found.</param>
		/// <returns><c>true</c> when the macro exists.</returns>
		public Boolean TryGetValue(String token, out ScriptMacro? macro)
		{
			return this.Macros.TryGetValue(token, out macro);
		}

		/// <summary>Replaces the content of the registry.</summary>
		/// <param name="macros">The new macros.</param>
		/// <param name="sourceDirectory">The directory the macros came from, if any.</param>
		public void Replace(IEnumerable<ScriptMacro> macros, String? sourceDirectory = null)
		{
			this._loadedStamps = null;
			var snapshot = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
			foreach (ScriptMacro macro in macros)
			{
				snapshot[macro.Token] = macro;
			}

			Volatile.Write(ref this._sourceDirectory, sourceDirectory);
			Volatile.Write(ref this._macros, snapshot);
		}

		/// <summary>Removes all macros.</summary>
		public void Clear()
		{
			this._loadedStamps = null;
			Volatile.Write(ref this._sourceDirectory, null);
			Volatile.Write(ref this._macros, _empty);
		}

		/// <summary>
		/// Reloads the registry from every <c>*.h</c> file below a directory (legacy <c>GetGlobalMacros</c>).
		/// </summary>
		/// <param name="directory">The headers directory.</param>
		/// <param name="diagnostics">Receives messages about missing directories or files; may be <c>null</c>.</param>
		/// <param name="useCache">
		/// <c>true</c> to keep the macros already loaded when the directory, the names, the sizes and the modification times of its
		/// header files are the same as at the last load (reading hundreds of headers takes a few hundred milliseconds, and every
		/// script that is opened asks for the macros); files modified in the last seconds are always read again.
		/// </param>
		/// <returns>
		/// The number of macros loaded, or -1 when the directory is unset or missing (the previous content is kept then).
		/// </returns>
		public Int32 LoadFromDirectory(String? directory, ParseDiagnostics? diagnostics = null, Boolean useCache = false)
		{
			lock (this._loadGate)
			{
				IReadOnlyList<String>? files = MacroCollector.FindHeaderFiles(directory, diagnostics);
				if (files is null)
				{
					return -1;
				}

				// The time stamps are read before the files: a file saved while the others are read is then seen as changed next time.
				HeaderStamp[]? stamps = ReadStamps(files);
				if (useCache && stamps is not null && this._loadedStamps is { } known && String.Equals(this._sourceDirectory, directory, StringComparison.Ordinal) && known.AsSpan().SequenceEqual(stamps))
				{
					return this.Macros.Count;
				}

				var collector = new MacroCollector(new IncludeResolver(null, null));
				var collected = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
				ParseDiagnostics own = new ParseDiagnostics();
				foreach (String file in files)
				{
					collector.CollectFromFile(file, collected, false, own);
				}

				if (diagnostics is not null)
				{
					foreach (ParseDiagnostic message in own)
					{
						diagnostics.Add(message.Severity, message.Source, message.Line, message.Message);
					}
				}

				Volatile.Write(ref this._sourceDirectory, directory);
				Volatile.Write(ref this._macros, collected);

				// A load that reported a problem (an unreadable header) or that read a file saved a moment ago is not worth remembering.
				this._loadedStamps = own.Count == 0 && stamps is not null && !IsRecent(stamps) ? stamps : null;
				return collected.Count;
			}
		}

		private static HeaderStamp[]? ReadStamps(IReadOnlyList<String> files)
		{
			HeaderStamp[] stamps = new HeaderStamp[files.Count];
			try
			{
				for (Int32 i = 0; i < stamps.Length; i++)
				{
					FileInfo info = new FileInfo(files[i]);
					stamps[i] = new HeaderStamp(files[i], info.LastWriteTimeUtc.Ticks, info.Length);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return null;
			}

			Array.Sort(stamps, (a, b) => String.CompareOrdinal(a.Path, b.Path));
			return stamps;
		}

		private static Boolean IsRecent(HeaderStamp[] stamps)
		{
			Int64 limit = (DateTime.UtcNow - _recentlyModified).Ticks;
			foreach (HeaderStamp stamp in stamps)
			{
				if (stamp.LastWriteTicks > limit)
				{
					return true;
				}
			}

			return false;
		}

		private readonly record struct HeaderStamp(String Path, Int64 LastWriteTicks, Int64 Length);
	}
}
