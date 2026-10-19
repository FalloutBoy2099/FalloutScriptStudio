namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>
	/// Supplies the dialogue function rules that are in effect (they can change while the program runs).
	/// </summary>
	public interface IDialogueRulesSource
	{
		/// <summary>Gets the current rules.</summary>
		DialogueFunctionRules Current { get; }
	}
}