namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	/// <summary>The marks of the dialogue check: the nodes with a problem are drawn with a badge, and the problems are listed.</summary>
	public sealed partial class FlowchartViewModel
	{
		private DialogueQualityReport _qualityReport = DialogueQualityReport.Empty;

		/// <summary>Gets the findings of the dialogue check, in script order.</summary>
		public ObservableCollection<FlowchartIssueViewModel> QualityIssues { get; } = [];

		/// <summary>Gets a value indicating whether the dialogue check found anything.</summary>
		public Boolean HasQualityIssues => this.QualityIssues.Count > 0;

		/// <summary>Gets the text of the button that opens the list of issues ("Issues (3)").</summary>
		public String QualityButtonText => Loc.Format("Str.Flowchart.Quality.Button", this.QualityIssues.Count);

		/// <summary>Gets a sentence that says how many issues the dialogue check found; a live region of the window.</summary>
		public String QualitySummary => this.QualityIssues.Count == 0 ? Loc.Get("Str.Flowchart.Quality.None") : Loc.Format("Str.Flowchart.Quality.Summary", this.QualityIssues.Count);

		/// <summary>Moves the view to the node of an issue and selects it.</summary>
		/// <param name="issue">The issue.</param>
		[RelayCommand]
		public void GoToIssue(FlowchartIssueViewModel? issue)
		{
			if (issue is not null)
			{
				this.JumpToNode(issue.Node);
			}
		}

		/// <summary>Checks the dialogue of the script again and marks the nodes of the canvas. Without an analyzer, or when the analysis fails, the nodes carry no mark.</summary>
		/// <returns>A task that completes when the nodes are marked.</returns>
		public async Task RefreshQualityAsync()
		{
			if (this._quality is null || this._script is null)
			{
				return;
			}

			DialogueQualityReport report;
			try
			{
				report = await this._quality.AnalyzeAsync(this._script);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or KeyNotFoundException or FormatException or OverflowException)
			{
				report = DialogueQualityReport.Empty;
			}

			this.ApplyQuality(report);
		}

		private void ApplyQuality(DialogueQualityReport report)
		{
			this._qualityReport = report;
			if (this._canvas is not null)
			{
				FlowchartQualityMapper.Apply(this._canvas.Nodes, report);
			}

			this.QualityIssues.Clear();
			foreach (DialogueQualityFinding finding in report.Findings)
			{
				this.QualityIssues.Add(new FlowchartIssueViewModel(finding));
			}

			this.OnPropertyChanged(nameof(this.HasQualityIssues));
			this.OnPropertyChanged(nameof(this.QualityButtonText));
			this.OnPropertyChanged(nameof(this.QualitySummary));
		}

		private void RefreshQualityTexts()
		{
			if (this._qualityReport.Findings.Count > 0 || this.QualityIssues.Count > 0)
			{
				this.ApplyQuality(this._qualityReport);
			}
			else
			{
				this.OnPropertyChanged(nameof(this.QualityButtonText));
				this.OnPropertyChanged(nameof(this.QualitySummary));
			}
		}
	}
}