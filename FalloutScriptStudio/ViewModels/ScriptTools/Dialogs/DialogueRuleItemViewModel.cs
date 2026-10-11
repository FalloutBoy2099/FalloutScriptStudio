namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// One editable row of the dialogue function rules grid. Argument positions are one-based and 0 means "not used",
	/// exactly as the rules file and the legacy grid write them.
	/// </summary>
	public sealed partial class DialogueRuleItemViewModel : ObservableObject
	{
		/// <summary>Gets the statement kinds a rule can have, as the first column of the grid offers them.</summary>
		public static IReadOnlyList<String> KindNames { get; } = new[] { "Option", "Reply", "Message", "Call" };

		/// <summary>Gets or sets the kind of statement: <c>Option</c>, <c>Reply</c>, <c>Message</c> or <c>Call</c>.</summary>
		[ObservableProperty]
		public partial String Kind { get; set; } = "Option";

		/// <summary>Gets or sets the function or macro name.</summary>
		[ObservableProperty]
		public partial String Name { get; set; } = String.Empty;

		/// <summary>Gets or sets the total number of arguments.</summary>
		[ObservableProperty]
		public partial Int32 TotalArgs { get; set; } = 1;

		/// <summary>Gets or sets the position of the message number argument.</summary>
		[ObservableProperty]
		public partial Int32 MessageArg { get; set; } = 1;

		/// <summary>Gets or sets the position of the target node argument, 0 when not used.</summary>
		[ObservableProperty]
		public partial Int32 NodeArg { get; set; }

		/// <summary>Gets or sets the position of the IQ argument, 0 when not used.</summary>
		[ObservableProperty]
		public partial Int32 IqArg { get; set; }

		/// <summary>Gets or sets the position of the message file argument, 0 when not used.</summary>
		[ObservableProperty]
		public partial Int32 FileArg { get; set; }

		/// <summary>Gets the problem with this row, or <see langword="null"/> when the row is valid.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasError))]
		public partial String? Error { get; private set; }

		/// <summary>Gets a value indicating whether the row has a problem.</summary>
		public Boolean HasError => this.Error is not null;

		/// <summary>Creates a row from a rule.</summary>
		/// <param name="rule">The rule.</param>
		/// <returns>The row.</returns>
		public static DialogueRuleItemViewModel FromRule(DialogueFunctionRule rule)
		{
			ArgumentNullException.ThrowIfNull(rule);

			return new DialogueRuleItemViewModel
			{
				Kind = rule.Opcode.ToRuleKind(),
				Name = rule.Name,
				TotalArgs = rule.TotalArgs,
				MessageArg = rule.MessageArg + 1,
				NodeArg = rule.NodeArg + 1,
				IqArg = rule.IqArg + 1,
				FileArg = rule.MessageFileArg + 1,
			};
		}

		/// <summary>Converts the row to a user rule.</summary>
		/// <returns>The rule.</returns>
		/// <exception cref="InvalidOperationException">The row is not valid.</exception>
		public DialogueFunctionRule ToRule()
		{
			DialogueOpcode opcode = DialogueOpcodeExtensions.ParseRuleKind(this.Kind);
			if (opcode == DialogueOpcode.None || DialogueRuleValidator.ValidateName(this.Name) is not null)
			{
				throw new InvalidOperationException("The row does not describe a valid rule.");
			}

			return DialogueFunctionRule.FromOneBased(opcode, this.Name, this.MessageArg, this.FileArg, this.NodeArg, this.IqArg, this.TotalArgs);
		}

		/// <summary>Checks this row apart from the other rows and stores the result in <see cref="Error"/>.</summary>
		/// <returns>The problem, or <see langword="null"/>.</returns>
		public String? ValidateRow()
		{
			String? error = DialogueRuleValidator.ValidateName(this.Name)
				?? DialogueRuleValidator.ValidateArguments(this.Kind, this.TotalArgs, this.MessageArg, this.NodeArg, this.IqArg, this.FileArg);
			this.Error = error;
			return error;
		}

		/// <summary>Marks the row as a duplicate of another one.</summary>
		public void MarkDuplicate()
		{
			this.Error = Loc.Format("Str.ScriptTools.RulesErrDuplicate", this.Name.Trim());
		}

		/// <summary>Gets a text that is equal for rows with equal content (used to detect unsaved changes).</summary>
		/// <returns>The key.</returns>
		public String CreateKey()
		{
			return String.Join(
				"|",
				this.Kind,
				this.Name.Trim(),
				this.TotalArgs.ToString(CultureInfo.InvariantCulture),
				this.MessageArg.ToString(CultureInfo.InvariantCulture),
				this.NodeArg.ToString(CultureInfo.InvariantCulture),
				this.IqArg.ToString(CultureInfo.InvariantCulture),
				this.FileArg.ToString(CultureInfo.InvariantCulture));
		}
	}
}
