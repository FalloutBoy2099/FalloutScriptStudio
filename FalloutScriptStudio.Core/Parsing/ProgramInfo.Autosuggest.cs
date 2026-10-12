namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The autocomplete list for the word being typed.</summary>
	public sealed partial class ProgramInfo
	{
		private const Int32 MaxMacroPreview = 300;

		/// <summary>
		/// Builds the autocomplete list for the word being typed (legacy <c>LookupAutosuggest</c>).
		/// </summary>
		/// <param name="part">The beginning of the word.</param>
		/// <param name="opcodes">Optional opcode catalog whose matches are listed first.</param>
		/// <returns>
		/// The matching opcodes, procedures, variables and macros (in this order) with duplicated names removed.
		/// </returns>
		public IReadOnlyList<AutosuggestItem> GetAutosuggest(String part, OpcodeCatalog? opcodes = null)
		{
			var matches = new List<AutosuggestItem>();
			if (opcodes is not null)
			{
				matches.AddRange(opcodes.Lookup(part));
			}

			String lower = part.ToLowerInvariant();
			foreach (KeyValuePair<String, ScriptProcedure> entry in this._procedureLookup)
			{
				if (entry.Key.StartsWith(lower, StringComparison.Ordinal))
				{
					matches.Add(new AutosuggestItem(entry.Value.Name, entry.Value.ShortDescription, AutosuggestKind.Procedure));
				}
			}

			foreach (KeyValuePair<String, ScriptVariable> entry in this._variableLookup)
			{
				if (entry.Key.StartsWith(lower, StringComparison.Ordinal))
				{
					matches.Add(new AutosuggestItem(entry.Value.Name, entry.Value.FullDescription, AutosuggestKind.Variable));
				}
			}

			var macros = new SortedDictionary<String, AutosuggestItem>(StringComparer.Ordinal);
			foreach (KeyValuePair<String, ScriptMacro> entry in this.ScriptMacros)
			{
				if (entry.Key.StartsWith(part, StringComparison.OrdinalIgnoreCase))
				{
					macros[entry.Value.DefinitionName] = CreateMacroItem(entry.Value);
				}
			}

			foreach (KeyValuePair<String, ScriptMacro> entry in this.GlobalMacros.Macros)
			{
				if (entry.Key.StartsWith(part, StringComparison.OrdinalIgnoreCase) && !macros.ContainsKey(entry.Value.DefinitionName))
				{
					macros[entry.Value.DefinitionName] = CreateMacroItem(entry.Value);
				}
			}

			matches.AddRange(macros.Values);

			var seen = new HashSet<String>(StringComparer.Ordinal);
			return matches.Where(m => seen.Add(m.Name)).ToList();
		}

		private static AutosuggestItem CreateMacroItem(ScriptMacro macro)
		{
			String preview = macro.Code.Length > MaxMacroPreview ? "No preview macros." : macro.Code;
			return new AutosuggestItem(macro.DefinitionName, "Define:\n" + preview, AutosuggestKind.Macro);
		}
	}
}
