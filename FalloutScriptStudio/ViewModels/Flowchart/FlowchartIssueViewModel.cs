namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using Controls.Diagram.Model;
	using Core.Dialogue;
	using Localization;
	using Services.ScriptTools.Flowchart;

	/// <summary>One finding of the dialogue check, as the list of issues of the flowchart window shows it. The texts are computed when they are read, so they follow a language change.</summary>
	public sealed class FlowchartIssueViewModel
	{
		/// <summary>Initializes a new instance of the <see cref="FlowchartIssueViewModel"/> class.</summary>
		/// <param name="finding">The finding.</param>
		public FlowchartIssueViewModel(DialogueQualityFinding finding)
		{
			ArgumentNullException.ThrowIfNull(finding);
			this.Finding = finding;
		}

		/// <summary>Gets the finding.</summary>
		public DialogueQualityFinding Finding { get; }

		/// <summary>Gets the name of the node the issue belongs to.</summary>
		public String Node => this.Finding.Node;

		/// <summary>Gets the severity, with the shape the badge of the node has.</summary>
		public DiagramIssueSeverity Severity => FlowchartQualityMapper.ToSeverity(this.Finding.Severity);

		/// <summary>Gets the severity as a word.</summary>
		public String SeverityText => this.Finding.Severity switch
		{
			DialogueQualitySeverity.Error => Loc.Get("Str.Common.Error"),
			DialogueQualitySeverity.Warning => Loc.Get("Str.Common.Warning"),
			_ => Loc.Get("Str.Common.Message"),
		};

		/// <summary>Gets the description of the issue.</summary>
		public String Text => FlowchartQualityMapper.Describe(this.Finding);

		/// <summary>Gets the name a screen reader reads: the severity, then the description.</summary>
		public String AccessibleName => this.SeverityText + ": " + this.Text;
	}
}