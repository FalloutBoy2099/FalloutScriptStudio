namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;

	/// <summary>
	/// The small modal dialogs of the dialogue flowchart.
	/// </summary>
	public interface IFlowchartDialogService
	{
		/// <summary>
		/// Shows the "new dialogue node" dialog (name and code template). The dialog creates the node in the script when the user
		/// confirms and stays open while the name is refused.
		/// </summary>
		/// <param name="script">The script that receives the node.</param>
		/// <returns>The name of the created node, or <see langword="null"/> when the user cancelled.</returns>
		Task<String?> ShowNewNodeAsync(DocumentViewModel script);

		/// <summary>Shows a dialog to edit the text of one message.</summary>
		/// <param name="caption">The text above the edit box, for example the message file.</param>
		/// <param name="text">The current text.</param>
		/// <returns>The new text, or <see langword="null"/> when the user cancelled.</returns>
		Task<String?> EditMessageTextAsync(String caption, String text);
	}
}
