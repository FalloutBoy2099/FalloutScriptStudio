namespace FalloutScriptStudio.Services.ScriptTools.Procedures
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Pure validation of the text typed into the procedure name dialog (legacy <c>ProcForm</c> closing checks),
	/// done live instead of when the dialog is closed.
	/// </summary>
	public static class ProcedureNameValidator
	{
		/// <summary>The hint shown in the empty name field.</summary>
		public const String ExampleName = "Example(arg0, arg1)";

		private const String VariableKeyword = "variable";

		private static readonly SearchValues<Char> _parentheses = SearchValues.Create("()");

		private static readonly HashSet<String> ReservedWords = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
		{
			"procedure", "variable", "import", "export", "in", "when", "pure", "inline", "begin", "end", "exit", "detach",
			"fork", "spawn", "callstart", "exec", "noop", "wait", "call", "cancel", "cancelall", "if", "then", "else",
			"switch", "case", "default", "return", "do", "while", "for", "foreach", "include", "startcritical",
			"endcritical", "critical", "break", "continue", "and", "or", "not", "bwand", "bwor", "bwxor", "bwnot", "mod",
		};

		/// <summary>Determines whether a word is a keyword of the script language and so cannot be a name.</summary>
		/// <param name="word">The word.</param>
		/// <returns><see langword="true"/> for reserved words.</returns>
		public static Boolean IsReservedWord(String word)
		{
			return ReservedWords.Contains(word);
		}

		/// <summary>Determines whether the text is an identifier: letters, digits and underscores, not starting with a digit.</summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="true"/> for identifiers.</returns>
		public static Boolean IsIdentifier(String? text)
		{
			if (String.IsNullOrEmpty(text) || Char.IsDigit(text[0]))
			{
				return false;
			}

			return text.All(IsIdentifierChar);
		}

		/// <summary>
		/// Increments the number at the end of a name, keeping its width (<c>proc007</c> becomes <c>proc008</c>); names without a number are returned unchanged.
		/// </summary>
		/// <param name="name">The name (without an argument list).</param>
		/// <returns>The incremented name.</returns>
		public static String IncrementTrailingNumber(String name)
		{
			Int32 start = name.Length;
			while (start > 0 && name[start - 1] >= '0' && name[start - 1] <= '9')
			{
				start--;
			}

			Int32 width = name.Length - start;
			if (width == 0 || width > 18)
			{
				return name;
			}

			Int64 number = Int64.Parse(name.AsSpan(start), NumberStyles.None, CultureInfo.InvariantCulture) + 1;
			return String.Concat(name.AsSpan(0, start), number.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Decides what the name field contains when the dialog opens: the suggested name when it is usable, otherwise nothing (the example is shown as a hint).
		/// </summary>
		/// <param name="candidate">The suggested name (selected text, current procedure...).</param>
		/// <param name="mode">How the dialog is used; creating from a procedure increments its trailing number.</param>
		/// <returns>The initial text.</returns>
		public static String PrepareInitialName(String? candidate, ProcedureNameDialogMode mode)
		{
			String name = (candidate ?? String.Empty).Trim();
			if (name.Length == 0 || name.Any(Char.IsWhiteSpace) || IsReservedWord(name))
			{
				return String.Empty;
			}

			if (mode != ProcedureNameDialogMode.Create)
			{
				return name;
			}

			Int32 open = name.IndexOf('(');
			return open < 0
				? IncrementTrailingNumber(name)
				: String.Concat(IncrementTrailingNumber(name.Substring(0, open)), name.AsSpan(open));
		}

		/// <summary>
		/// Validates the text of the name field.
		/// </summary>
		/// <param name="input">The text.</param>
		/// <param name="mode">How the dialog is used. Only creating accepts an argument list.</param>
		/// <param name="existingNames">The names that are already taken.</param>
		/// <param name="originalName">When renaming, the current name, which may be kept (or changed in case only).</param>
		/// <returns>The validation result.</returns>
		public static ProcedureNameValidation Validate(String? input, ProcedureNameDialogMode mode, IReadOnlyCollection<String> existingNames, String? originalName = null)
		{
			ArgumentNullException.ThrowIfNull(existingNames);

			String text = (input ?? String.Empty).Trim();
			if (text.Length == 0)
			{
				return Reject(null, String.Empty);
			}

			Int32 open = text.IndexOf('(');
			if (open >= 0 && mode == ProcedureNameDialogMode.Rename)
			{
				return Reject(Loc.Get("Str.ScriptTools.ProcErrInvalidChars"), text);
			}

			String name = (open < 0 ? text : text.Substring(0, open)).Trim();
			String? error = ValidateIdentifier(name, false);
			if (error is not null)
			{
				return Reject(error, name);
			}

			Boolean isOriginal = mode == ProcedureNameDialogMode.Rename
				&& originalName is not null
				&& String.Equals(originalName, name, StringComparison.OrdinalIgnoreCase);
			if (!isOriginal && existingNames.Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				return Reject(
					Loc.Format(mode == ProcedureNameDialogMode.Rename ? "Str.ScriptTools.ProcErrNameInUse" : "Str.ScriptTools.ProcErrAlreadyExists", name),
					name);
			}

			if (open < 0)
			{
				return new ProcedureNameValidation(true, null, name, name);
			}

			Int32 close = text.LastIndexOf(')');
			if (close < open)
			{
				return Reject(Loc.Get("Str.ScriptTools.ProcErrMissingParen"), name);
			}

			if (text.Substring(close + 1).Trim().Length > 0)
			{
				return Reject(Loc.Get("Str.ScriptTools.ProcErrTextAfterParen"), name);
			}

			String inner = text.Substring(open + 1, close - open - 1);
			if (inner.AsSpan().IndexOfAny(_parentheses) >= 0)
			{
				return Reject(Loc.Get("Str.ScriptTools.ProcErrNestedParens"), name);
			}

			if (inner.Trim().Length == 0)
			{
				return new ProcedureNameValidation(true, null, name, name + "()");
			}

			List<String> arguments = new List<String>();
			foreach (String part in inner.Split(','))
			{
				String argument = StripVariableKeyword(part.Trim());
				if (argument.Length == 0)
				{
					return Reject(Loc.Get("Str.ScriptTools.ProcErrArgMissing"), name);
				}

				error = ValidateIdentifier(argument, true);
				if (error is not null)
				{
					return Reject(error, name);
				}

				if (arguments.Contains(argument, StringComparer.OrdinalIgnoreCase))
				{
					return Reject(Loc.Format("Str.ScriptTools.ProcErrArgDuplicate", argument), name);
				}

				arguments.Add(argument);
			}

			String declaration = name + "(" + String.Join(", ", arguments.Select(argument => VariableKeyword + " " + argument)) + ")";
			return new ProcedureNameValidation(true, null, name, declaration);
		}

		private static ProcedureNameValidation Reject(String? error, String name)
		{
			return new ProcedureNameValidation(false, error, name, String.Empty);
		}

		private static Boolean IsIdentifierChar(Char c)
		{
			return Char.IsLetterOrDigit(c) || c == '_';
		}

		private static String StripVariableKeyword(String argument)
		{
			if (argument.Length > VariableKeyword.Length
				&& argument.StartsWith(VariableKeyword, StringComparison.OrdinalIgnoreCase)
				&& Char.IsWhiteSpace(argument[VariableKeyword.Length]))
			{
				return argument.Substring(VariableKeyword.Length).Trim();
			}

			return argument;
		}

		private static String? ValidateIdentifier(String text, Boolean isArgument)
		{
			if (text.Length == 0)
			{
				return Loc.Get("Str.ScriptTools.ProcErrNeedName");
			}

			if (text.Any(Char.IsWhiteSpace))
			{
				return Loc.Get(isArgument ? "Str.ScriptTools.ProcErrArgNoSpaces" : "Str.ScriptTools.ProcErrNameNoSpaces");
			}

			if (!text.All(IsIdentifierChar))
			{
				return isArgument ? Loc.Format("Str.ScriptTools.ProcErrArgInvalidChars", text) : Loc.Get("Str.ScriptTools.ProcErrInvalidChars");
			}

			if (Char.IsDigit(text[0]))
			{
				return Loc.Get(isArgument ? "Str.ScriptTools.ProcErrArgStartsDigit" : "Str.ScriptTools.ProcErrNameStartsDigit");
			}

			if (IsReservedWord(text))
			{
				return Loc.Format("Str.ScriptTools.ProcErrReserved", text);
			}

			return null;
		}
	}
}
