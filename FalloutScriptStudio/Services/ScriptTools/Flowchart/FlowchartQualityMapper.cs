namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Controls.Diagram.Model;
	using Core.Dialogue;
	using Problems;

	/// <summary>Marks the nodes of the flowchart with the findings of the dialogue check: a ring and a badge of the worst severity, a faded node when nothing leads to it, and the list of the findings as the tool tip.</summary>
	public static class FlowchartQualityMapper
	{
		/// <summary>Marks the nodes of the canvas; a node without a finding loses its mark.</summary>
		/// <param name="nodes">The nodes of the canvas.</param>
		/// <param name="report">The report of the check.</param>
		public static void Apply(IEnumerable<DiagramNode> nodes, DialogueQualityReport report)
		{
			ArgumentNullException.ThrowIfNull(nodes);
			ArgumentNullException.ThrowIfNull(report);

			foreach (DiagramNode node in nodes)
			{
				List<DialogueQualityFinding> findings = report.ForNode(node.Name).ToList();
				node.IssueSeverity = findings.Count == 0 ? DiagramIssueSeverity.None : findings.Select(finding => ToSeverity(finding.Severity)).Max();
				node.IsDimmed = findings.Any(finding => finding.Code == DialogueQualityCode.UnreachableNode);
				node.IssueText = String.Join(Environment.NewLine, findings.Select(Describe));
			}
		}

		/// <summary>Gets the text of a finding in the language of the interface (the text of the matching problem).</summary>
		/// <param name="finding">The finding.</param>
		/// <returns>The text.</returns>
		public static String Describe(DialogueQualityFinding finding)
		{
			ArgumentNullException.ThrowIfNull(finding);
			return ProblemFactory.DialogueQuality(finding, "script", null).Message;
		}

		/// <summary>Converts the severity of a finding to the one of the canvas.</summary>
		/// <param name="severity">The severity of the finding.</param>
		/// <returns>The severity of the badge.</returns>
		public static DiagramIssueSeverity ToSeverity(DialogueQualitySeverity severity)
		{
			return severity switch
			{
				DialogueQualitySeverity.Error => DiagramIssueSeverity.Error,
				DialogueQualitySeverity.Warning => DiagramIssueSeverity.Warning,
				_ => DiagramIssueSeverity.Info,
			};
		}
	}
}