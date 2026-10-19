namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	/// <summary>The outcome of a "Replace in Files" run.</summary>
	/// <param name="Kind">How the run ended.</param>
	/// <param name="Plan">The preview that was shown (also when it was cancelled).</param>
	/// <param name="Result">What was replaced, or <see langword="null"/> when nothing was applied.</param>
	public sealed record ReplaceOutcome(ReplaceOutcomeKind Kind, ReplacePlan Plan, ReplaceResult? Result);
}