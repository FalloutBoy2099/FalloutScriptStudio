namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The set of dialogue functions the parser understands: the built-in engine functions plus the user's rules
	/// (legacy <c>DialogFunctionsRules.opcodeTemplates</c>). Instances are immutable; names are compared case-insensitively
	/// (script identifiers are case-insensitive).
	/// </summary>
	public sealed class DialogueFunctionRules
	{
		private static readonly IReadOnlyList<DialogueFunctionRule> _builtIn = new[]
		{
			DialogueFunctionRule.FromOneBased(DialogueOpcode.GsayReply, "gsay_reply", messageArg: 2, messageFileArg: 1, totalArgs: 2, isDefault: true),
			DialogueFunctionRule.FromOneBased(DialogueOpcode.GsayMessage, "gsay_message", messageArg: 2, messageFileArg: 1, totalArgs: 2, isDefault: true),
			DialogueFunctionRule.FromOneBased(DialogueOpcode.GsayOption, "gsay_option", messageArg: 2, messageFileArg: 1, nodeArg: 3, totalArgs: 4, isDefault: true),
			DialogueFunctionRule.FromOneBased(DialogueOpcode.GiqOption, "giq_option", messageArg: 3, messageFileArg: 2, nodeArg: 4, iqArg: 1, totalArgs: 5, isDefault: true),
		};

		private readonly List<DialogueFunctionRule> _rules;
		private readonly Dictionary<String, DialogueFunctionRule> _byName;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueFunctionRules"/> class.
		/// </summary>
		/// <param name="userRules">
		/// The user's rules. A rule whose name is already taken (by an earlier rule or by a built-in function) is ignored.
		/// </param>
		public DialogueFunctionRules(IEnumerable<DialogueFunctionRule>? userRules = null)
		{
			this._rules = [];
			this._byName = new(StringComparer.OrdinalIgnoreCase);

			foreach (DialogueFunctionRule rule in _builtIn)
			{
				this._byName.Add(rule.Name, rule);
			}

			var user = new List<DialogueFunctionRule>();
			foreach (DialogueFunctionRule rule in userRules ?? Enumerable.Empty<DialogueFunctionRule>())
			{
				DialogueFunctionRule userRule = rule.IsDefault ? rule.AsUserRule() : rule;
				if (this._byName.TryAdd(userRule.Name, userRule))
				{
					user.Add(userRule);
				}
			}

			this._rules.AddRange(user);
			this._rules.AddRange(_builtIn);
		}

		/// <summary>Gets the built-in rules for the engine functions <c>gsay_reply</c>, <c>gsay_message</c>, <c>gsay_option</c> and <c>giq_option</c>.</summary>
		public static IReadOnlyList<DialogueFunctionRule> BuiltIn => _builtIn;

		/// <summary>Gets all rules: the user's rules first, then the built-in ones.</summary>
		public IReadOnlyList<DialogueFunctionRule> Rules => this._rules;

		/// <summary>Gets only the user's rules.</summary>
		public IEnumerable<DialogueFunctionRule> UserRules => this._rules.Where(r => !r.IsDefault);

		/// <summary>Finds the rule of a function.</summary>
		/// <param name="name">The function or macro name (case-insensitive).</param>
		/// <param name="rule">The rule, when found.</param>
		/// <returns><c>true</c> when the function has a rule.</returns>
		public Boolean TryGet(String name, out DialogueFunctionRule rule)
		{
			ArgumentNullException.ThrowIfNull(name);

			if (this._byName.TryGetValue(name, out DialogueFunctionRule? found))
			{
				rule = found;
				return true;
			}

			rule = null!;
			return false;
		}

		/// <summary>Returns a copy with other user rules (the built-in rules are kept).</summary>
		/// <param name="userRules">The new user rules.</param>
		/// <returns>The new rule set.</returns>
		public DialogueFunctionRules WithUserRules(IEnumerable<DialogueFunctionRule> userRules)
		{
			return new DialogueFunctionRules(userRules);
		}
	}
}