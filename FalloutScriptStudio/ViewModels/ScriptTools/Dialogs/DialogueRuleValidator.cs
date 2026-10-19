namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Checks the fields of a dialogue function rule the way the dialogue parser needs them: a wrong argument position
	/// would make the parser read outside the argument list (the legacy dialog only warned that this "will crash the program").
	/// </summary>
	public static class DialogueRuleValidator
	{
		/// <summary>The largest number of arguments a rule can describe.</summary>
		public const Int32 MaxArguments = 32;

		/// <summary>Determines whether a text is a function or macro name: a letter or an underscore followed by letters, digits or underscores.</summary>
		/// <param name="name">The name.</param>
		/// <returns><see langword="true"/> for an identifier.</returns>
		public static Boolean IsValidName(String? name)
		{
			if (String.IsNullOrEmpty(name) || !IsNameStart(name[0]))
			{
				return false;
			}

			foreach (Char c in name)
			{
				if (!Char.IsLetterOrDigit(c) && c != '_')
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Checks the name of a rule, apart from the other rules.</summary>
		/// <param name="name">The name (surrounding white space is ignored).</param>
		/// <returns>The problem, or <see langword="null"/> when the name is acceptable.</returns>
		public static String? ValidateName(String? name)
		{
			String trimmed = (name ?? String.Empty).Trim();
			if (trimmed.Length == 0)
			{
				return Loc.Get("Str.ScriptTools.RulesErrNameEmpty");
			}

			if (!IsValidName(trimmed))
			{
				return Loc.Get("Str.ScriptTools.RulesErrNameChars");
			}

			foreach (DialogueFunctionRule builtIn in DialogueFunctionRules.BuiltIn)
			{
				if (String.Equals(builtIn.Name, trimmed, StringComparison.OrdinalIgnoreCase))
				{
					return Loc.Format("Str.ScriptTools.RulesErrBuiltIn", builtIn.Name);
				}
			}

			return null;
		}

		/// <summary>Checks the argument positions of a rule.</summary>
		/// <param name="kind">The kind of statement: <c>Option</c>, <c>Reply</c>, <c>Message</c> or <c>Call</c>.</param>
		/// <param name="totalArgs">The number of arguments of the function.</param>
		/// <param name="messageArg">The one-based position of the message number, 0 when not used.</param>
		/// <param name="nodeArg">The one-based position of the target node, 0 when not used.</param>
		/// <param name="iqArg">The one-based position of the IQ, 0 when not used.</param>
		/// <param name="fileArg">The one-based position of the message file, 0 when not used.</param>
		/// <returns>The problem, or <see langword="null"/> when the positions are consistent.</returns>
		public static String? ValidateArguments(String? kind, Int32 totalArgs, Int32 messageArg, Int32 nodeArg, Int32 iqArg, Int32 fileArg)
		{
			DialogueOpcode opcode = DialogueOpcodeExtensions.ParseRuleKind(kind ?? String.Empty);
			if (opcode == DialogueOpcode.None)
			{
				return Loc.Get("Str.ScriptTools.RulesErrChooseType");
			}

			if (totalArgs < 1 || totalArgs > MaxArguments)
			{
				return Loc.Format("Str.ScriptTools.RulesErrArgCount", MaxArguments);
			}

			(String Column, Int32 Value)[] positions =
			{
				(Loc.Get("Str.ScriptTools.RulesColMessage"), messageArg),
				(Loc.Get("Str.ScriptTools.RulesColNode"), nodeArg),
				(Loc.Get("Str.ScriptTools.RulesColIq"), iqArg),
				(Loc.Get("Str.ScriptTools.RulesColFile"), fileArg),
			};
			foreach ((String column, Int32 value) in positions)
			{
				if (value < 0 || value > totalArgs)
				{
					return Loc.Format("Str.ScriptTools.RulesErrPosition", column, totalArgs);
				}
			}

			if (opcode != DialogueOpcode.CallMacro && messageArg == 0)
			{
				return Loc.Get("Str.ScriptTools.RulesErrMessageRequired");
			}

			if ((opcode == DialogueOpcode.Option || opcode == DialogueOpcode.CallMacro) && nodeArg == 0)
			{
				return Loc.Get("Str.ScriptTools.RulesErrNodeRequired");
			}

			HashSet<Int32> used = [];
			foreach ((String column, Int32 value) in positions)
			{
				if (value != 0 && !used.Add(value))
				{
					return Loc.Format("Str.ScriptTools.RulesErrPositionUsed", column, value);
				}
			}

			return null;
		}

		private static Boolean IsNameStart(Char c)
		{
			return Char.IsLetter(c) || c == '_';
		}
	}
}