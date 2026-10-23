namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>
	/// The variables the user does not want findings for. An entry is a name (an sfall global, a macro), a number, or a prefix that ends with
	/// <c>*</c>; the case is ignored. It is compared with the names, the macros and the number of a variable.
	/// </summary>
	public sealed class GlobalIgnoreList
	{
		private readonly HashSet<String> _exact = new(StringComparer.OrdinalIgnoreCase);
		private readonly List<String> _prefixes = [];

		/// <summary>Initializes a new instance of the <see cref="GlobalIgnoreList"/> class.</summary>
		/// <param name="entries">The entries; blank ones are skipped.</param>
		public GlobalIgnoreList(IEnumerable<String> entries)
		{
			ArgumentNullException.ThrowIfNull(entries);
			foreach (String raw in entries)
			{
				String entry = raw.Trim();
				if (entry.Length == 0)
				{
					continue;
				}

				if (entry.EndsWith('*'))
				{
					String prefix = entry.TrimEnd('*');
					if (prefix.Length > 0)
					{
						this._prefixes.Add(prefix);
					}
				}
				else
				{
					this._exact.Add(entry);
				}
			}
		}

		/// <summary>Gets a value indicating whether the list has no entry.</summary>
		public Boolean IsEmpty => this._exact.Count == 0 && this._prefixes.Count == 0;

		/// <summary>Determines whether a variable is in the list.</summary>
		/// <param name="variable">The variable.</param>
		/// <returns><see langword="true"/> when one of its names or its number matches an entry.</returns>
		public Boolean Matches(GlobalVariable variable)
		{
			ArgumentNullException.ThrowIfNull(variable);
			if (this.IsEmpty)
			{
				return false;
			}

			if (variable.Id is { } id && this.MatchesText(id.ToString(CultureInfo.InvariantCulture)))
			{
				return true;
			}

			foreach (String name in variable.Names)
			{
				if (this.MatchesText(name))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>Determines whether a text (a name or a number) is in the list.</summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="true"/> when it matches an entry.</returns>
		public Boolean MatchesText(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			if (this._exact.Contains(text))
			{
				return true;
			}

			foreach (String prefix in this._prefixes)
			{
				if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}