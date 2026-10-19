namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>One rule of <see cref="ScriptDiagnostics"/>: its code, its name (the last part of the localization keys) and its usual severity.</summary>
	/// <param name="Code">The stable code, for example <c>SSL1001</c>.</param>
	/// <param name="Name">The name of the rule, for example <c>WrongArgumentCount</c>.</param>
	/// <param name="Severity">The severity the rule reports (a rule that is not certain inside a conditional block reports a lower one).</param>
	public sealed record ScriptDiagnosticRule(String Code, String Name, ScriptDiagnosticSeverity Severity)
	{
		/// <summary>The prefix of the localization keys of the messages (<c>Str.Editor.Diag.</c>).</summary>
		public const String KeyPrefix = "Str.Editor.Diag.";

		/// <summary>Gets the localization key of the message.</summary>
		public String MessageKey => KeyPrefix + this.Name;

		/// <summary>Gets the localization key of the hint.</summary>
		public String HintKey => KeyPrefix + this.Name + "Hint";
	}
}