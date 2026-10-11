namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	/// <summary>
	/// The text format of the user's dialogue rules (<c>DialogFunc.cfg</c>): one rule per line,
	/// <c>Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ</c> with one-based argument positions (0 = not used).
	/// Lines that are empty or start with <c>;</c> are comments.
	/// </summary>
	public static class DialogueFunctionRulesFile
	{
		/// <summary>The comment line written at the top of the file.</summary>
		public const String HeaderLine = ";Format: Type, Name, CountArgs, MsgLine, MsgFile, Node, IQ";

		/// <summary>Parses the lines of a rules file.</summary>
		/// <param name="lines">The lines.</param>
		/// <returns>The user rules; malformed lines are skipped instead of failing the whole file as the legacy reader did.</returns>
		public static IReadOnlyList<DialogueFunctionRule> Parse(IEnumerable<String> lines)
		{
			ArgumentNullException.ThrowIfNull(lines);

			var rules = new List<DialogueFunctionRule>();
			foreach (String raw in lines)
			{
				String line = raw.TrimStart();
				if (line.Length == 0 || line[0] == ';')
				{
					continue;
				}

				String[] fields = line.Split(',');
				if (fields.Length < 7)
				{
					continue;
				}

				DialogueOpcode opcode = DialogueOpcodeExtensions.ParseRuleKind(fields[0]);
				if (opcode == DialogueOpcode.None || fields[1].Trim().Length == 0)
				{
					continue;
				}

				Int32[] numbers = new Int32[5];
				Boolean valid = true;
				for (Int32 i = 0; i < numbers.Length; i++)
				{
					valid &= Int32.TryParse(fields[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]);
				}

				if (!valid || numbers[0] < 1 || numbers.Any(n => n < 0))
				{
					continue;
				}

				rules.Add(DialogueFunctionRule.FromOneBased(opcode, fields[1], messageArg: numbers[1], messageFileArg: numbers[2], nodeArg: numbers[3], iqArg: numbers[4], totalArgs: numbers[0]));
			}

			return rules;
		}

		/// <summary>Formats the user rules as the lines of a rules file.</summary>
		/// <param name="rules">The rules; built-in rules are skipped.</param>
		/// <returns>The lines, starting with the format comment.</returns>
		public static IReadOnlyList<String> Format(IEnumerable<DialogueFunctionRule> rules)
		{
			ArgumentNullException.ThrowIfNull(rules);

			var lines = new List<String> { HeaderLine };
			foreach (DialogueFunctionRule rule in rules.Where(r => !r.IsDefault))
			{
				lines.Add(String.Format(
					CultureInfo.InvariantCulture,
					"{0}, {1}, {2}, {3}, {4}, {5}, {6}",
					rule.Opcode.ToRuleKind(),
					rule.Name,
					rule.TotalArgs,
					rule.MessageArg + 1,
					rule.MessageFileArg + 1,
					rule.NodeArg + 1,
					rule.IqArg + 1));
			}

			return lines;
		}
	}
}
