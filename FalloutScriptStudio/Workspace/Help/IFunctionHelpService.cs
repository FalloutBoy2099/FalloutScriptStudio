namespace FalloutScriptStudio.Workspace.Help
{
	using System;
	using System.Collections.Generic;
	using Core.Help;
	using Core.Parsing;

	/// <summary>
	/// The documentation of the function, opcode, macro or procedure under the caret: what the description files say about the built-in
	/// functions, what the source says about the symbols of the project (the comment above them), and the page of the sfall documentation.
	/// </summary>
	public interface IFunctionHelpService
	{
		/// <summary>Raised after the topics were made again (the functions tree or the description files changed).</summary>
		event EventHandler? Changed;

		/// <summary>Gets the topics of the description files and of the functions tree.</summary>
		FunctionHelpCatalog Catalog { get; }

		/// <summary>Finds the help of a word of a script.</summary>
		/// <param name="word">The word under the caret.</param>
		/// <param name="info">The parse result of the script, for the macros, procedures and variables of the project; may be <see langword="null"/>.</param>
		/// <param name="file">The full path of the script, or <see langword="null"/>.</param>
		/// <param name="line">The 1-based line of the word (finds the variables of a procedure).</param>
		/// <returns>The topic, or <see langword="null"/> when nothing is known about the word.</returns>
		HelpTopic? Find(String word, ProgramInfo? info, String? file, Int32 line);

		/// <summary>Finds the built-in topics that match a filter, the best first.</summary>
		/// <param name="filter">The filter text; every topic by name when blank.</param>
		/// <param name="maxResults">The most topics to return.</param>
		/// <returns>The topics.</returns>
		IReadOnlyList<HelpTopic> Search(String? filter, Int32 maxResults);

		/// <summary>Opens the page of the sfall documentation of a topic in the browser; only the address of the documentation site is ever opened.</summary>
		/// <param name="topic">The topic.</param>
		/// <returns>The address that was opened, or <see langword="null"/> when the topic has no page or the browser could not be started.</returns>
		Uri? OpenDocumentation(HelpTopic topic);
	}
}