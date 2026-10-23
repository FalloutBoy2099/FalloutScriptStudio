namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	/// <summary>
	/// Every read and write of a global variable of the project, grouped by variable: the sfall globals (by name or by number), the game
	/// variables (<c>GVAR_*</c>), the map variables and the local variables of the scripts. The macros of the project that stand for the
	/// number or the name of a variable are resolved (<c>global_var(GVAR_FOO)</c> and <c>global_var(27)</c> are the same variable when
	/// <c>GVAR_FOO</c> is 27); a name that cannot be resolved stays symbolic. The index is immutable: the project index builds a new one
	/// when its files change.
	/// </summary>
	public sealed class GlobalVariableIndex
	{
		private const Int32 MaxAliasDepth = 8;

		private readonly Dictionary<String, GlobalVariable[]> _byName;

		private GlobalVariableIndex(IReadOnlyList<GlobalVariable> variables, Int32 useCount, Int32 fileCount, Boolean isTruncated)
		{
			this.Variables = variables;
			this.UseCount = useCount;
			this.FileCount = fileCount;
			this.IsTruncated = isTruncated;

			Dictionary<String, List<GlobalVariable>> names = new(StringComparer.OrdinalIgnoreCase);
			foreach (GlobalVariable variable in variables)
			{
				foreach (String name in variable.Names.Append(variable.Key).Distinct(StringComparer.OrdinalIgnoreCase))
				{
					if (!names.TryGetValue(name, out List<GlobalVariable>? list))
					{
						list = new(1);
						names.Add(name, list);
					}

					list.Add(variable);
				}
			}

			this._byName = names.ToDictionary(static p => p.Key, static p => p.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>Gets an index without any use.</summary>
		public static GlobalVariableIndex Empty { get; } = new GlobalVariableIndex(Array.Empty<GlobalVariable>(), 0, 0, false);

		/// <summary>Gets the variables ordered by family, then by name (the numbered ones last, in numeric order).</summary>
		public IReadOnlyList<GlobalVariable> Variables { get; }

		/// <summary>Gets the number of uses of all the variables.</summary>
		public Int32 UseCount { get; }

		/// <summary>Gets the number of files that use a global variable.</summary>
		public Int32 FileCount { get; }

		/// <summary>Gets a value indicating whether some files were left out because the project holds more uses than the index keeps.</summary>
		public Boolean IsTruncated { get; }

		/// <summary>Gets every use of every variable.</summary>
		public IEnumerable<GlobalVariableUse> Uses => this.Variables.SelectMany(static v => v.Uses);

		/// <summary>Builds an index from the text of some files (used by the tools and the tests that have no project index).</summary>
		/// <param name="files">The full path and the text of each file.</param>
		/// <returns>The index.</returns>
		public static GlobalVariableIndex FromTexts(IEnumerable<KeyValuePair<String, String>> files)
		{
			ArgumentNullException.ThrowIfNull(files);

			GlobalStringPool pool = new();
			List<KeyValuePair<String, FileGlobalScan>> scans = [];
			foreach (KeyValuePair<String, String> file in files)
			{
				FileGlobalScan scan = GlobalUseScanner.ScanText(file.Value, pool);
				if (!scan.IsEmpty)
				{
					scans.Add(new KeyValuePair<String, FileGlobalScan>(file.Key, scan));
				}
			}

			return Build(scans, false);
		}

		/// <summary>Finds the variables with a name: the name of an sfall global, or a macro.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <returns>The variables (several when a name is used for variables of different families or scripts).</returns>
		public IReadOnlyList<GlobalVariable> FindByName(String name)
		{
			ArgumentNullException.ThrowIfNull(name);
			return this._byName.TryGetValue(name, out GlobalVariable[]? found) ? found : Array.Empty<GlobalVariable>();
		}

		/// <summary>Builds the index from the scans of the files.</summary>
		/// <param name="files">The scan of each file by full path.</param>
		/// <param name="isTruncated">Whether some files were left out.</param>
		/// <returns>The index.</returns>
		internal static GlobalVariableIndex Build(IEnumerable<KeyValuePair<String, FileGlobalScan>> files, Boolean isTruncated)
		{
			KeyValuePair<String, FileGlobalScan>[] ordered = files.Where(static f => !f.Value.IsEmpty).ToArray();
			if (ordered.Length == 0)
			{
				return isTruncated ? new GlobalVariableIndex(Array.Empty<GlobalVariable>(), 0, 0, true) : Empty;
			}

			Array.Sort(ordered, static (a, b) => String.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
			Dictionary<String, MacroConstant> shared = new(StringComparer.Ordinal);
			HashSet<String> ambiguous = new(StringComparer.Ordinal);
			foreach (KeyValuePair<String, FileGlobalScan> file in ordered)
			{
				foreach (MacroConstant constant in file.Value.Constants)
				{
					if (shared.TryGetValue(constant.Name, out MacroConstant known))
					{
						if (known.Kind != constant.Kind || !String.Equals(known.Value, constant.Value, StringComparison.Ordinal))
						{
							ambiguous.Add(constant.Name);
						}
					}
					else
					{
						shared.Add(constant.Name, constant);
					}
				}
			}

			Dictionary<String, Group> groups = new(StringComparer.Ordinal);
			Int32 useCount = 0;
			Int32 fileCount = 0;
			foreach (KeyValuePair<String, FileGlobalScan> file in ordered)
			{
				if (file.Value.Uses.Length == 0)
				{
					continue;
				}

				fileCount++;
				Dictionary<String, MacroConstant>? own = file.Value.Constants.Length == 0 ? null : file.Value.Constants.ToDictionary(static c => c.Name, StringComparer.Ordinal);
				foreach (RawGlobalUse raw in file.Value.Uses)
				{
					GlobalVariableUse use = CreateUse(raw, file.Key, file.Value, own, shared, ambiguous, out Resolved resolved);
					String groupKey = resolved.GroupKey(raw.Function.Kind, file.Key);
					if (!groups.TryGetValue(groupKey, out Group? group))
					{
						group = new Group(raw.Function.Kind, resolved, raw.Function.Kind == GlobalVariableKind.LocalVariable ? file.Key : null);
						groups.Add(groupKey, group);
					}

					group.Add(use, resolved);
					useCount++;
				}
			}

			GlobalVariable[] variables = groups.Values.Select(static g => g.ToVariable()).ToArray();
			Array.Sort(variables, CompareVariables);
			return new GlobalVariableIndex(variables, useCount, fileCount, isTruncated);
		}

		private static Int32 CompareVariables(GlobalVariable a, GlobalVariable b)
		{
			Int32 result = a.Kind.CompareTo(b.Kind);
			if (result != 0)
			{
				return result;
			}

			Boolean aNumber = a.KeyKind == GlobalKeyKind.Id && a.Aliases.Count == 0;
			Boolean bNumber = b.KeyKind == GlobalKeyKind.Id && b.Aliases.Count == 0;
			if (aNumber != bNumber)
			{
				return aNumber ? 1 : -1;
			}

			if (aNumber)
			{
				result = Nullable.Compare(a.Id, b.Id);
			}
			else
			{
				result = String.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
			}

			if (result == 0)
			{
				result = String.Compare(a.Scope, b.Scope, StringComparison.OrdinalIgnoreCase);
			}

			return result;
		}

		private static GlobalVariableUse CreateUse(
			RawGlobalUse raw,
			String file,
			FileGlobalScan scan,
			Dictionary<String, MacroConstant>? own,
			Dictionary<String, MacroConstant> shared,
			HashSet<String> ambiguous,
			out Resolved resolved)
		{
			GlobalFunction function = raw.Function;
			resolved = Resolve(raw, function.Kind, own, shared, ambiguous);

			String? literal = null;
			GlobalValueType type = function.ValueType;
			if (raw.Value is not null)
			{
				String text = raw.Value;
				if (raw.ValueIsSymbol)
				{
					text = ResolveNumber(raw.Value, own, shared, ambiguous) ?? String.Empty;
				}

				if (text.Length > 0)
				{
					literal = text;
					if (function.Kind == GlobalVariableKind.SfallGlobal && type == GlobalValueType.Unknown)
					{
						if (GlobalNumbers.IsFloat(text))
						{
							type = GlobalValueType.Float;
						}
						else if (GlobalNumbers.TryParseInteger(text, out _))
						{
							type = GlobalValueType.Int;
						}
					}
				}
			}

			String? procedure = raw.Procedure >= 0 && raw.Procedure < scan.Procedures.Length ? scan.Procedures[raw.Procedure] : null;
			return new GlobalVariableUse(function.Name, function.Kind, raw.Key, function.Access, file, raw.Line, procedure, type, literal);
		}

		private static String? ResolveNumber(String name, Dictionary<String, MacroConstant>? own, Dictionary<String, MacroConstant> shared, HashSet<String> ambiguous)
		{
			String current = name;
			for (Int32 depth = 0; depth < MaxAliasDepth; depth++)
			{
				if (!TryLookup(current, own, shared, ambiguous, out MacroConstant constant))
				{
					return null;
				}

				switch (constant.Kind)
				{
					case MacroConstantKind.Number:
						return constant.Value;
					case MacroConstantKind.Alias:
						current = constant.Value;
						break;
					default:
						return null;
				}
			}

			return null;
		}

		private static Boolean TryLookup(String name, Dictionary<String, MacroConstant>? own, Dictionary<String, MacroConstant> shared, HashSet<String> ambiguous, out MacroConstant constant)
		{
			if (own is not null && own.TryGetValue(name, out constant))
			{
				return true;
			}

			if (!ambiguous.Contains(name) && shared.TryGetValue(name, out constant))
			{
				return true;
			}

			constant = default;
			return false;
		}

		private static Resolved Resolve(RawGlobalUse raw, GlobalVariableKind kind, Dictionary<String, MacroConstant>? own, Dictionary<String, MacroConstant> shared, HashSet<String> ambiguous)
		{
			if (raw.KeyKind == GlobalKeyKind.Name)
			{
				return new Resolved(kind, GlobalKeyKind.Name, raw.Key, null, null);
			}

			if (raw.KeyKind == GlobalKeyKind.Id)
			{
				GlobalNumbers.TryParseInteger(raw.Key, out Int64 number);
				return new Resolved(kind, GlobalKeyKind.Id, raw.Key, number, null);
			}

			String current = raw.Key;
			for (Int32 depth = 0; depth < MaxAliasDepth; depth++)
			{
				if (!TryLookup(current, own, shared, ambiguous, out MacroConstant constant))
				{
					break;
				}

				if (constant.Kind == MacroConstantKind.Alias)
				{
					current = constant.Value;
					continue;
				}

				if (constant.Kind == MacroConstantKind.String)
				{
					return kind == GlobalVariableKind.SfallGlobal
						? new Resolved(kind, GlobalKeyKind.Name, constant.Value, null, raw.Key)
						: new Resolved(kind, GlobalKeyKind.Symbol, raw.Key, null, null);
				}

				if (GlobalNumbers.TryParseInteger(constant.Value, out Int64 id))
				{
					return new Resolved(kind, GlobalKeyKind.Id, id.ToString(CultureInfo.InvariantCulture), id, raw.Key);
				}

				break;
			}

			return new Resolved(kind, GlobalKeyKind.Symbol, raw.Key, null, null);
		}

		/// <summary>The identity of the variable a use refers to once the macros are resolved.</summary>
		private readonly record struct Resolved(GlobalVariableKind Kind, GlobalKeyKind KeyKind, String Key, Int64? Id, String? Alias)
		{
			/// <summary>Gets the key that two uses of the same variable share. A map variable is known by its macro when it has one: the numbers of different maps overlap.</summary>
			internal String GroupKey(GlobalVariableKind kind, String file)
			{
				String scope = kind == GlobalVariableKind.LocalVariable ? file : String.Empty;
				String identity;
				if (this.Kind == GlobalVariableKind.MapVariable && this.Alias is not null)
				{
					identity = "s:" + this.Alias;
				}
				else
				{
					identity = (this.KeyKind switch
					{
						GlobalKeyKind.Name => "n:",
						GlobalKeyKind.Id => "i:",
						_ => "s:",
					}) + this.Key;
				}

				return ((Int32)kind).ToString(CultureInfo.InvariantCulture) + "|" + scope + "|" + identity;
			}
		}

		private sealed class Group
		{
			private readonly GlobalVariableKind _kind;
			private readonly Resolved _first;
			private readonly String? _scope;
			private readonly List<GlobalVariableUse> _uses = [];
			private readonly Dictionary<String, Int32> _aliases = new(StringComparer.Ordinal);

			internal Group(GlobalVariableKind kind, Resolved first, String? scope)
			{
				this._kind = kind;
				this._first = first;
				this._scope = scope;
			}

			internal void Add(GlobalVariableUse use, Resolved resolved)
			{
				this._uses.Add(use);
				if (resolved.Alias is not null)
				{
					this._aliases[resolved.Alias] = this._aliases.GetValueOrDefault(resolved.Alias) + 1;
				}
			}

			internal GlobalVariable ToVariable()
			{
				String[] aliases = this._aliases
					.OrderByDescending(static p => p.Value)
					.ThenBy(static p => p.Key, StringComparer.Ordinal)
					.Select(static p => p.Key)
					.ToArray();
				GlobalVariableUse[] uses = this._uses.ToArray();
				Array.Sort(uses, static (a, b) =>
				{
					Int32 result = String.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
					return result != 0 ? result : a.Line.CompareTo(b.Line);
				});

				return new GlobalVariable(this._kind, this._first.KeyKind, this._first.Key, this._first.Id, this._scope, aliases, uses);
			}
		}
	}
}