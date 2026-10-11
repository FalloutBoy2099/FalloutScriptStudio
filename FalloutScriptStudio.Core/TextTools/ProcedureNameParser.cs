namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// Checks and normalizes the procedure name typed in the "new procedure" / "rename" dialog (legacy <c>ProcForm</c> logic).
	/// </summary>
	public static class ProcedureNameParser
	{
		private static readonly String[] _reservedNames = { "procedure", "begin", "end", "variable" };

		/// <summary>
		/// Parses a name, optionally followed by an argument list.
		/// </summary>
		/// <param name="input">The text typed by the user, for example <c>Helper(a, b)</c> or <c>Helper(variable a)</c>.</param>
		/// <param name="allowArguments">Whether an argument list is accepted (renaming accepts a bare name only).</param>
		/// <returns>
		/// The parsed name. Every argument gets the <c>variable</c> keyword when it is missing:
		/// <c>Helper(a, b)</c> becomes <c>Helper(variable a, variable b)</c>.
		/// </returns>
		public static ParsedProcedureName Parse(String input, Boolean allowArguments)
		{
			ArgumentNullException.ThrowIfNull(input);

			String name = input.Trim();
			if (name.Length == 0)
			{
				return Invalid("The name is empty.");
			}

			Int32 paren = name.IndexOf('(');
			if (paren > 0)
			{
				Int32 keyword = paren;
				while (true)
				{
					keyword = name.IndexOf("variable ", keyword, StringComparison.OrdinalIgnoreCase);
					if (keyword == -1)
					{
						break;
					}

					name = name.Remove(keyword, 9);
				}
			}

			name = name.Replace(" ", String.Empty, StringComparison.Ordinal);
			paren = name.IndexOf('(');
			String bare = paren > -1 ? name.Substring(0, paren) : name;
			if (bare.Length == 0 || !IsIdentifier(bare))
			{
				return Invalid("The name can contain only letters, digits and underscores.");
			}

			if (Array.Exists(_reservedNames, r => String.Equals(r, bare, StringComparison.OrdinalIgnoreCase)))
			{
				return Invalid("The name is a reserved word.");
			}

			if (paren < 0)
			{
				return new ParsedProcedureName(true, bare, bare, null);
			}

			if (!allowArguments)
			{
				return Invalid("The name cannot contain arguments.");
			}

			Int32 pairs = 0;
			Int32 closed = 0;
			for (Int32 i = paren; i < name.Length; i++)
			{
				Char ch = name[i];
				if (ch == ',')
				{
					continue;
				}

				if (pairs > 0 && ch == ')')
				{
					closed++;
				}

				if (ch == '(')
				{
					pairs++;
				}

				if (ch == ')')
				{
					pairs--;
				}

				if (ch == '(' || ch == ')')
				{
					continue;
				}

				if (!WordScanner.IsWordChar(ch))
				{
					return Invalid("The arguments can contain only letters, digits and underscores.");
				}
			}

			if (pairs != 0 || closed > 1)
			{
				return Invalid("The parentheses of the argument list do not match.");
			}

			Int32 close = name.LastIndexOf(')');
			if (close < paren)
			{
				return Invalid("The argument list is not closed.");
			}

			var arguments = new List<String>();
			foreach (String argument in name.Substring(paren + 1, close - paren - 1).Split(','))
			{
				if (argument.Length > 0)
				{
					arguments.Add("variable " + argument);
				}
			}

			String declaration = arguments.Count == 0 ? bare : bare + "(" + String.Join(", ", arguments) + ")";
			return new ParsedProcedureName(true, bare, declaration, null);
		}

		/// <summary>
		/// Increments the number at the end of a name, keeping its width (<c>Node009</c> becomes <c>Node010</c>);
		/// used to suggest the name of the next procedure.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns>The incremented name, or the name unchanged when it does not end with a digit.</returns>
		public static String IncrementTrailingNumber(String name)
		{
			ArgumentNullException.ThrowIfNull(name);

			if (name.Length == 0 || !Char.IsDigit(name[name.Length - 1]))
			{
				return name;
			}

			Int32 start = name.Length;
			while (start > 0 && Char.IsDigit(name[start - 1]))
			{
				start--;
			}

			String digits = name.Substring(start);
			if (!Int64.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out Int64 number))
			{
				return name;
			}

			var result = new StringBuilder(name.Substring(0, start));
			result.Append((number + 1).ToString(new String('0', digits.Length), System.Globalization.CultureInfo.InvariantCulture));
			return result.ToString();
		}

		/// <summary>Determines whether a string is a valid identifier (letters, digits and underscores).</summary>
		/// <param name="name">The string.</param>
		/// <returns><c>true</c> when every character is an identifier character.</returns>
		public static Boolean IsIdentifier(String name)
		{
			ArgumentNullException.ThrowIfNull(name);

			foreach (Char c in name)
			{
				if (!WordScanner.IsWordChar(c))
				{
					return false;
				}
			}

			return name.Length > 0;
		}

		private static ParsedProcedureName Invalid(String error)
		{
			return new ParsedProcedureName(false, String.Empty, String.Empty, error);
		}
	}
}
