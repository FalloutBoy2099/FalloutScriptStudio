namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	/// <summary>Shows the preview of "Replace in Files" and lets the user choose which replacements are made.</summary>
	public interface IReplacePreviewPresenter
	{
		/// <summary>Shows the preview.</summary>
		/// <param name="plan">The replacements that would be made.</param>
		/// <param name="request">What was asked (the text to find and the replacement, shown above the list).</param>
		/// <returns>The replacements the user ticked per file, or <see langword="null"/> when the preview was cancelled.</returns>
		Task<IReadOnlyList<ReplaceSelection>?> ShowAsync(ReplacePlan plan, ReplaceInFilesRequest request);
	}
}