namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;

	/// <summary>The result of checking a snippet.</summary>
	/// <param name="Trigger">What is wrong with the trigger.</param>
	/// <param name="BodyProblems">The mistakes of the body.</param>
	public sealed record SnippetValidation(SnippetTriggerIssue Trigger, IReadOnlyList<SnippetProblem> BodyProblems)
	{
		/// <summary>Gets a value indicating whether the snippet can be saved: the trigger is fine and the body has no mistake.</summary>
		public Boolean IsValid => this.Trigger == SnippetTriggerIssue.None && this.BodyProblems.Count == 0;
	}
}