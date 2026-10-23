namespace FalloutScriptStudio.Workspace
{
	using System.Threading;
	using System.Threading.Tasks;
	using Core.Dialogue;
	using Documents;

	/// <summary>Analyses the dialogue of a script (the flowchart marks the nodes with the findings); nothing is reported as a problem.</summary>
	public interface IDialogueQualityAnalyzer
	{
		/// <summary>Checks the dialogue of a script: the text as it is in the editor, the parse result, and the message file when the script has one.</summary>
		/// <param name="script">The script.</param>
		/// <param name="cancellationToken">Cancels the analysis.</param>
		/// <returns>The report; <see cref="DialogueQualityReport.Empty"/> when the script cannot be analysed.</returns>
		Task<DialogueQualityReport> AnalyzeAsync(DocumentViewModel script, CancellationToken cancellationToken = default);
	}
}