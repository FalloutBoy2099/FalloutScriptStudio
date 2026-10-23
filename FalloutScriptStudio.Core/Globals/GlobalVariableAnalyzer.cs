namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// Looks for the usual mistakes with global variables in a <see cref="GlobalVariableIndex"/>: a value that is written and never read
	/// or read and never written, an sfall global used as an integer and as a float, a name that is probably a typo of another one, a name
	/// that sfall cannot use (it needs exactly 8 characters, case-sensitive) and two long names that start alike. It is pure and does not
	/// keep the index.
	/// </summary>
	public static class GlobalVariableAnalyzer
	{
		/// <summary>The number of characters of the name of an sfall global (<c>functions.ini</c>: an 8-character, case-sensitive string is required).</summary>
		public const Int32 SfallNameLength = 8;

		private const Int32 MinSimilarLength = 4;
		private const Int32 MaxNamesPerGroup = 20_000;
		private const Int32 MaxBucketSize = 40;

		/// <summary>Analyzes the variables.</summary>
		/// <param name="index">The index.</param>
		/// <param name="options">What to leave out; the defaults when <see langword="null"/>.</param>
		/// <returns>The findings ordered by file and line.</returns>
		public static IReadOnlyList<GlobalFinding> Analyze(GlobalVariableIndex index, GlobalAnalysisOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(index);
			options ??= new GlobalAnalysisOptions();

			GlobalIgnoreList ignore = new(options.Ignored);
			List<GlobalVariable> variables = index.Variables.Where(v => !ignore.Matches(v)).ToList();
			List<GlobalFinding> findings = [];
			foreach (GlobalVariable variable in variables)
			{
				CheckUsage(variable, options, findings);
				if (variable.Kind == GlobalVariableKind.SfallGlobal)
				{
					CheckType(variable, findings);
					CheckLength(variable, findings);
				}
			}

			CheckPrefixes(variables, findings);
			CheckSimilarNames(variables, findings);

			findings.Sort(static (a, b) =>
			{
				Int32 result = String.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
				if (result == 0)
				{
					result = a.Line.CompareTo(b.Line);
				}

				return result != 0 ? result : a.Kind.CompareTo(b.Kind);
			});
			return findings.Count > options.MaxFindings ? findings.GetRange(0, Math.Max(options.MaxFindings, 0)) : findings;
		}

		private static void CheckUsage(GlobalVariable variable, GlobalAnalysisOptions options, List<GlobalFinding> findings)
		{
			// The uses of a local variable in a header belong to the scripts that include it: nothing can be said from the header alone.
			if (variable.Kind == GlobalVariableKind.LocalVariable && variable.Scope is { } scope && String.Equals(Path.GetExtension(scope), ".h", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (variable.WriteCount > 0 && variable.ReadCount == 0)
			{
				GlobalVariableUse use = variable.Uses.First(static u => u.Writes);
				findings.Add(Create(GlobalFindingKind.WrittenNeverRead, GlobalFindingSeverity.Info, variable, null, variable.WriteCount, use));
			}
			else if (variable.ReadCount > 0 && variable.WriteCount == 0 && !(options.SkipEngineVariables && GlobalEngineVariables.IsEngineProvided(variable)))
			{
				GlobalVariableUse use = variable.Uses.First(static u => u.Reads);
				findings.Add(Create(GlobalFindingKind.ReadNeverWritten, GlobalFindingSeverity.Warning, variable, null, variable.ReadCount, use));
			}
		}

		private static void CheckType(GlobalVariable variable, List<GlobalFinding> findings)
		{
			if (!variable.HasIntUses || !variable.HasFloatUses)
			{
				return;
			}

			Int32 ints = variable.Uses.Count(static u => u.ValueType == GlobalValueType.Int);
			Int32 floats = variable.Uses.Count(static u => u.ValueType == GlobalValueType.Float);
			GlobalValueType minority = floats <= ints ? GlobalValueType.Float : GlobalValueType.Int;
			GlobalVariableUse use = variable.Uses.First(u => u.ValueType == minority);
			findings.Add(Create(GlobalFindingKind.TypeConflict, GlobalFindingSeverity.Warning, variable, null, Math.Min(ints, floats), use));
		}

		private static void CheckLength(GlobalVariable variable, List<GlobalFinding> findings)
		{
			if (variable.KeyKind == GlobalKeyKind.Name && variable.Key.Length != SfallNameLength)
			{
				findings.Add(Create(GlobalFindingKind.NameLength, GlobalFindingSeverity.Warning, variable, null, variable.Key.Length, variable.Uses[0]));
			}
		}

		private static void CheckPrefixes(List<GlobalVariable> variables, List<GlobalFinding> findings)
		{
			Dictionary<String, List<GlobalVariable>> byPrefix = new(StringComparer.Ordinal);
			foreach (GlobalVariable variable in variables)
			{
				if (variable.Kind == GlobalVariableKind.SfallGlobal && variable.KeyKind == GlobalKeyKind.Name && variable.Key.Length > SfallNameLength)
				{
					String prefix = variable.Key.Substring(0, SfallNameLength);
					if (!byPrefix.TryGetValue(prefix, out List<GlobalVariable>? list))
					{
						list = [];
						byPrefix.Add(prefix, list);
					}

					list.Add(variable);
				}
			}

			foreach (List<GlobalVariable> group in byPrefix.Values.Where(static g => g.Count > 1))
			{
				GlobalVariable first = group[0];
				for (Int32 i = 1; i < group.Count; i++)
				{
					findings.Add(Create(GlobalFindingKind.PrefixCollision, GlobalFindingSeverity.Warning, group[i], first.DisplayName, SfallNameLength, group[i].Uses[0]));
				}
			}
		}

		private static void CheckSimilarNames(List<GlobalVariable> variables, List<GlobalFinding> findings)
		{
			foreach (IGrouping<(GlobalVariableKind, String?), GlobalVariable> group in variables.GroupBy(static v => (v.Kind, v.Scope)))
			{
				Dictionary<String, GlobalVariable> byName = new(StringComparer.Ordinal);
				foreach (GlobalVariable variable in group)
				{
					foreach (String name in variable.Names)
					{
						if (name.Length >= MinSimilarLength && byName.Count < MaxNamesPerGroup)
						{
							byName.TryAdd(name, variable);
						}
					}
				}

				FindSimilar(byName, findings);
			}
		}

		private static void FindSimilar(Dictionary<String, GlobalVariable> byName, List<GlobalFinding> findings)
		{
			String[] names = byName.Keys.OrderBy(static n => n, StringComparer.Ordinal).ToArray();
			String[] lower = names.Select(static n => n.ToLowerInvariant()).ToArray();
			HashSet<(Int32, Int32)> reported = [];
			Dictionary<String, List<Int32>> buckets = new(StringComparer.Ordinal);
			for (Int32 i = 0; i < lower.Length; i++)
			{
				AddToBucket(buckets, lower[i], i);
				for (Int32 c = 0; c < lower[i].Length; c++)
				{
					AddToBucket(buckets, lower[i].Remove(c, 1), i);
				}
			}

			foreach (List<Int32> bucket in buckets.Values)
			{
				if (bucket.Count < 2 || bucket.Count > MaxBucketSize)
				{
					continue;
				}

				for (Int32 x = 0; x < bucket.Count; x++)
				{
					for (Int32 y = x + 1; y < bucket.Count; y++)
					{
						Int32 a = bucket[x];
						Int32 b = bucket[y];
						if (!reported.Add((a, b)) || ReferenceEquals(byName[names[a]], byName[names[b]]) || !IsProbableTypo(lower[a], lower[b]))
						{
							continue;
						}

						GlobalVariable first = byName[names[a]];
						GlobalVariable second = byName[names[b]];
						Boolean firstIsMistake = first.Uses.Count < second.Uses.Count || (first.Uses.Count == second.Uses.Count && String.CompareOrdinal(names[a], names[b]) > 0);
						GlobalVariable mistake = firstIsMistake ? first : second;
						String suggestion = firstIsMistake ? names[b] : names[a];
						String written = firstIsMistake ? names[a] : names[b];
						GlobalVariableUse use = mistake.Uses.FirstOrDefault(u => String.Equals(u.Written, written, StringComparison.Ordinal)) ?? mistake.Uses[0];
						findings.Add(Create(GlobalFindingKind.SimilarName, GlobalFindingSeverity.Info, mistake, suggestion, mistake.Uses.Count, use, written));
					}
				}
			}
		}

		private static void AddToBucket(Dictionary<String, List<Int32>> buckets, String key, Int32 index)
		{
			if (!buckets.TryGetValue(key, out List<Int32>? list))
			{
				list = new(2);
				buckets.Add(key, list);
			}

			if (list.Count == 0 || list[^1] != index)
			{
				list.Add(index);
			}
		}

		/// <summary>Determines whether two names (in lower case, or equal ignoring the case) look like one name and its typo.</summary>
		/// <param name="a">The first name in lower case.</param>
		/// <param name="b">The second name in lower case.</param>
		/// <returns><see langword="true"/> when the names differ by one character that is not a digit, or by two neighbouring characters.</returns>
		internal static Boolean IsProbableTypo(String a, String b)
		{
			if (a.Length < MinSimilarLength || b.Length < MinSimilarLength)
			{
				return false;
			}

			if (String.Equals(a, b, StringComparison.Ordinal))
			{
				return true;
			}

			if (a.Length == b.Length)
			{
				Int32 first = -1;
				Int32 last = -1;
				Int32 differences = 0;
				for (Int32 i = 0; i < a.Length; i++)
				{
					if (a[i] != b[i])
					{
						differences++;
						first = first < 0 ? i : first;
						last = i;
					}
				}

				if (differences == 1)
				{
					return !Char.IsDigit(a[first]) && !Char.IsDigit(b[first]);
				}

				return differences == 2 && last == first + 1 && a[first] == b[last] && a[last] == b[first] && !Char.IsDigit(a[first]) && !Char.IsDigit(a[last]);
			}

			if (Math.Abs(a.Length - b.Length) != 1)
			{
				return false;
			}

			String longer = a.Length > b.Length ? a : b;
			String shorter = a.Length > b.Length ? b : a;
			Int32 at = 0;
			while (at < shorter.Length && longer[at] == shorter[at])
			{
				at++;
			}

			return String.Equals(longer.Remove(at, 1), shorter, StringComparison.Ordinal) && !Char.IsDigit(longer[at]);
		}

		private static GlobalFinding Create(GlobalFindingKind kind, GlobalFindingSeverity severity, GlobalVariable variable, String? other, Int32 number, GlobalVariableUse use, String? shown = null)
		{
			return new GlobalFinding(kind, severity, shown ?? variable.DisplayName, other, number, use.File, use.Line, use.Procedure);
		}
	}
}