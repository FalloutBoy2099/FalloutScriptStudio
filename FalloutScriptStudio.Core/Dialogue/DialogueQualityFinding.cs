namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>One problem of the structure of a dialogue.</summary>
	/// <param name="Code">What is reported.</param>
	/// <param name="Severity">How serious it is.</param>
	/// <param name="Node">The node the finding belongs to (the first node of a group for <see cref="DialogueQualityCode.ExitlessCycle"/>).</param>
	/// <param name="Line">The 1-based line of the script to go to: the statement when there is one, otherwise the line of the node.</param>
	/// <param name="RelatedNode">The other node the finding is about (the target of a duplicate option, the undefined procedure, another node of the cycle), or <see langword="null"/>.</param>
	/// <param name="Count">The count the finding is about: the options of the node, the nodes of the cycle, the missing numbers; 0 otherwise.</param>
	/// <param name="Numbers">The message numbers the finding is about (the duplicate number, the missing numbers); empty otherwise.</param>
	/// <param name="AffectedNodes">Every node the finding is about, the first being <paramref name="Node"/>; the flowchart marks all of them.</param>
	public sealed record DialogueQualityFinding(
		DialogueQualityCode Code,
		DialogueQualitySeverity Severity,
		String Node,
		Int32 Line,
		String? RelatedNode,
		Int32 Count,
		IReadOnlyList<Int32> Numbers,
		IReadOnlyList<String> AffectedNodes)
	{
		/// <summary>Gets the identifier of the finding as shown to the user (<c>DLG001</c>...).</summary>
		public String Id => "DLG" + ((Int32)this.Code).ToString("D3", CultureInfo.InvariantCulture);

		/// <summary>Determines whether the finding is about a node.</summary>
		/// <param name="node">The name of the node (the case is ignored).</param>
		/// <returns><see langword="true"/> when the node is one of <see cref="AffectedNodes"/>.</returns>
		public Boolean Affects(String node)
		{
			ArgumentNullException.ThrowIfNull(node);
			foreach (String affected in this.AffectedNodes)
			{
				if (String.Equals(affected, node, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}