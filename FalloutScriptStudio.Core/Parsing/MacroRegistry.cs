namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
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

		private IReadOnlyDictionary<String, ScriptMacro> _macros = _empty;
		private String? _sourceDirectory;

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
			Volatile.Write(ref this._sourceDirectory, null);
			Volatile.Write(ref this._macros, _empty);
		}

		/// <summary>
		/// Reloads the registry from every <c>*.h</c> file below a directory (legacy <c>GetGlobalMacros</c>).
		/// </summary>
		/// <param name="directory">The headers directory.</param>
		/// <param name="diagnostics">Receives messages about missing directories or files; may be <c>null</c>.</param>
		/// <returns>
		/// The number of macros loaded, or -1 when the directory is unset or missing (the previous content is kept then).
		/// </returns>
		public Int32 LoadFromDirectory(String? directory, ParseDiagnostics? diagnostics = null)
		{
			IReadOnlyList<String>? files = MacroCollector.FindHeaderFiles(directory, diagnostics);
			if (files is null)
			{
				return -1;
			}

			var collector = new MacroCollector(new IncludeResolver(null, null));
			var collected = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
			foreach (String file in files)
			{
				collector.CollectFromFile(file, collected, false, diagnostics);
			}

			Volatile.Write(ref this._sourceDirectory, directory);
			Volatile.Write(ref this._macros, collected);
			return collected.Count;
		}
	}
}
