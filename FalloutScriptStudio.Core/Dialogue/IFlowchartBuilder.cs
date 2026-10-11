namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Builds the nodes and links of the dialogue flowchart from a script (the data part of the legacy <c>NodeDiagram</c>).
	/// </summary>
	public interface IFlowchartBuilder
	{
		/// <summary>
		/// Parses every dialogue node procedure of a script, loads the message files they use and works out the links between nodes.
		/// </summary>
		/// <param name="scriptText">The text of the script.</param>
		/// <param name="info">The parse result of the script.</param>
		/// <param name="messages">The message files of the script.</param>
		/// <param name="options">The display options.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>
		/// The model. A node gets <see cref="DialogueNodeKind.DialogStart"/> for <c>talk_p_proc</c>, <see cref="DialogueNodeKind.DialogEnd"/>
		/// when only others link to it, <see cref="DialogueNodeKind.NoFromLink"/> when it only links to others, and
		/// <see cref="DialogueNodeKind.Unused"/> when it has no links at all.
		/// </returns>
		Task<FlowchartModel> BuildAsync(String scriptText, ProgramInfo info, IDialogueMessageSource messages, FlowchartBuildOptions options, CancellationToken cancellationToken = default);
	}
}
