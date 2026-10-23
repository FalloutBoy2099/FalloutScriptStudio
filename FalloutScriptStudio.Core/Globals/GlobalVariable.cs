namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>One global variable of the project with every place that reads or writes it.</summary>
	public sealed class GlobalVariable
	{
		private readonly String _displayName;

		internal GlobalVariable(
			GlobalVariableKind kind,
			GlobalKeyKind keyKind,
			String key,
			Int64? id,
			String? scope,
			IReadOnlyList<String> aliases,
			IReadOnlyList<GlobalVariableUse> uses)
		{
			this.Kind = kind;
			this.KeyKind = keyKind;
			this.Key = key;
			this.Id = id;
			this.Scope = scope;
			this.Aliases = aliases;
			this.Uses = uses;

			Int32 reads = 0;
			Int32 writes = 0;
			Boolean hasInt = false;
			Boolean hasFloat = false;
			HashSet<String> files = new(StringComparer.OrdinalIgnoreCase);
			foreach (GlobalVariableUse use in uses)
			{
				if (use.Reads)
				{
					reads++;
				}

				if (use.Writes)
				{
					writes++;
				}

				hasInt |= use.ValueType == GlobalValueType.Int;
				hasFloat |= use.ValueType == GlobalValueType.Float;
				files.Add(use.File);
			}

			this.ReadCount = reads;
			this.WriteCount = writes;
			this.HasIntUses = hasInt;
			this.HasFloatUses = hasFloat;
			this.FileCount = files.Count;

			String? primary = aliases.Count > 0 ? aliases[0] : null;
			if (keyKind == GlobalKeyKind.Name)
			{
				this._displayName = key;
			}
			else if (primary is not null)
			{
				this._displayName = id is { } number ? primary + " (" + number.ToString(CultureInfo.InvariantCulture) + ")" : primary;
			}
			else
			{
				this._displayName = key;
			}
		}

		/// <summary>Gets the family of variables.</summary>
		public GlobalVariableKind Kind { get; }

		/// <summary>Gets how the variable is identified: by its name (an sfall global), by its number, or by a macro name that could not be resolved.</summary>
		public GlobalKeyKind KeyKind { get; }

		/// <summary>Gets the name (<see cref="GlobalKeyKind.Name"/>), the decimal number (<see cref="GlobalKeyKind.Id"/>) or the macro name (<see cref="GlobalKeyKind.Symbol"/>).</summary>
		public String Key { get; }

		/// <summary>Gets the number of the variable when it is known (a macro resolved to it, or the number written in the script).</summary>
		public Int64? Id { get; }

		/// <summary>Gets the script a local variable belongs to (its full path), or <see langword="null"/> for the other families.</summary>
		public String? Scope { get; }

		/// <summary>Gets the macros the variable is written with, the most used first.</summary>
		public IReadOnlyList<String> Aliases { get; }

		/// <summary>Gets every use, ordered by file and line.</summary>
		public IReadOnlyList<GlobalVariableUse> Uses { get; }

		/// <summary>Gets the number of uses that read the value.</summary>
		public Int32 ReadCount { get; }

		/// <summary>Gets the number of uses that write the value.</summary>
		public Int32 WriteCount { get; }

		/// <summary>Gets the number of files that use the variable.</summary>
		public Int32 FileCount { get; }

		/// <summary>Gets a value indicating whether some use reads or writes an integer.</summary>
		public Boolean HasIntUses { get; }

		/// <summary>Gets a value indicating whether some use reads or writes a floating point number.</summary>
		public Boolean HasFloatUses { get; }

		/// <summary>Gets the text to show: the name, the macro with its number (<c>GVAR_FOO (27)</c>), or the number.</summary>
		public String DisplayName => this._displayName;

		/// <summary>Gets the names the variable can be searched with: the name of an sfall global, or the macros.</summary>
		public IEnumerable<String> Names
		{
			get
			{
				if (this.KeyKind != GlobalKeyKind.Id)
				{
					yield return this.Key;
				}

				foreach (String alias in this.Aliases)
				{
					yield return alias;
				}
			}
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Kind + " " + this.DisplayName;
		}
	}
}