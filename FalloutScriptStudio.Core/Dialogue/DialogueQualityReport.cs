namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The result of <see cref="DialogueQualityChecker"/> for one script.</summary>
	/// <param name="Findings">The findings, in script order.</param>
	/// <param name="NodeCount">The number of dialogue nodes of the script.</param>
	/// <param name="EntryNode">The entry node of the dialogue (<c>talk_p_proc</c>), or <see langword="null"/> when the script has none.</param>
	/// <param name="HasDialogue">Whether the script has at least one node with a dialogue statement; a script without any has no findings.</param>
	public sealed record DialogueQualityReport(IReadOnlyList<DialogueQualityFinding> Findings, Int32 NodeCount, String? EntryNode, Boolean HasDialogue)
	{
		/// <summary>Gets an empty report (a script without a dialogue).</summary>
		public static DialogueQualityReport Empty { get; } = new DialogueQualityReport(Array.Empty<DialogueQualityFinding>(), 0, null, false);

		/// <summary>Gets the findings that are about a node.</summary>
		/// <param name="node">The name of the node (the case is ignored).</param>
		/// <returns>The findings, in script order.</returns>
		public IEnumerable<DialogueQualityFinding> ForNode(String node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return this.Findings.Where(finding => finding.Affects(node));
		}

		/// <summary>Gets the findings of one kind.</summary>
		/// <param name="code">The kind.</param>
		/// <returns>The findings.</returns>
		public IEnumerable<DialogueQualityFinding> Of(DialogueQualityCode code)
		{
			return this.Findings.Where(finding => finding.Code == code);
		}
	}
}