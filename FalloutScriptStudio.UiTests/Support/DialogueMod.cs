namespace FalloutScriptStudio.UiTests.Support
{
	using System;

	/// <summary>The files of the dialogue mod that <see cref="Fixtures.CreateDialogueMod"/> creates.</summary>
	/// <param name="Root">The folder of the mod (the project folder).</param>
	/// <param name="Scripts">The folder with the script, its header and <c>scripts.lst</c> (the output folder of the program).</param>
	/// <param name="Script">The dialogue script.</param>
	/// <param name="Header">The header of the script.</param>
	/// <param name="ScriptsList">The <c>scripts.lst</c> file.</param>
	/// <param name="EnglishMessages">The English message file.</param>
	/// <param name="FrenchMessages">The French translation.</param>
	/// <param name="Headers">The folder with the headers of the mod (the headers folder of the program).</param>
	/// <param name="ScriptsHeader">The <c>scripts.h</c> file of the mod.</param>
	/// <param name="NameMessages">The <c>scrname.msg</c> file with the in-game names of the scripts.</param>
	/// <param name="UglyScript">A script whose formatting the formatter changes.</param>
	internal sealed record DialogueMod(
		String Root,
		String Scripts,
		String Script,
		String Header,
		String ScriptsList,
		String EnglishMessages,
		String FrenchMessages,
		String Headers,
		String ScriptsHeader,
		String NameMessages,
		String UglyScript);
}