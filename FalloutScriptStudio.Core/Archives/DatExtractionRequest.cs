namespace FalloutScriptStudio.Core.Archives
{
	using System;

	/// <summary>
	/// Describes the extraction of one compiled script from a Fallout <c>.dat</c> archive.
	/// </summary>
	public sealed record DatExtractionRequest
	{
		/// <summary>Gets the full path of the <c>.dat</c> archive, or of the folder that replaces it.</summary>
		public required String DatFilePath { get; init; }

		/// <summary>Gets the name of the script inside the archive's <c>scripts</c> folder (for example <c>test.int</c>).</summary>
		public required String ScriptFileName { get; init; }

		/// <summary>Gets the folder the script is extracted to (the scratch folder).</summary>
		public required String OutputDirectory { get; init; }
	}
}
