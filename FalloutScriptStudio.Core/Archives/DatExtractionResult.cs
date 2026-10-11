namespace FalloutScriptStudio.Core.Archives
{
	using System;

	/// <summary>
	/// The outcome of a <c>.dat</c> extraction.
	/// </summary>
	/// <param name="Success">Whether the script was extracted.</param>
	/// <param name="ExtractedFilePath">The extracted file, or <see langword="null"/> on failure.</param>
	/// <param name="Message">A short description of the failure; empty on success.</param>
	public sealed record DatExtractionResult(Boolean Success, String? ExtractedFilePath, String Message);
}
