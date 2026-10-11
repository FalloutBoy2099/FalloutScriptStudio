namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Holds the dialogue function rules and keeps the user's rules in <c>DialogFunc.cfg</c> (legacy <c>DialogFunctionsRules</c>).
	/// </summary>
	public interface IDialogueRulesService : IDialogueRulesSource
	{
		/// <summary>Occurs after <see cref="IDialogueRulesSource.Current"/> was replaced.</summary>
		event EventHandler? Changed;

		/// <summary>Reads the user's rules from the rules file; a missing file gives only the built-in rules.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The rules.</returns>
		Task<DialogueFunctionRules> LoadAsync(CancellationToken cancellationToken = default);

		/// <summary>Replaces the user's rules and writes the rules file (atomically).</summary>
		/// <param name="userRules">The new user rules.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the file is written.</returns>
		Task SaveAsync(IEnumerable<DialogueFunctionRule> userRules, CancellationToken cancellationToken = default);
	}
}
