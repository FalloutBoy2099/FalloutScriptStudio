namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using FalloutScriptStudio.TestSupport;

	/// <summary>A uniquely named scratch folder in <c>_test_files\work</c> that is deleted when disposed.</summary>
	internal sealed class TempDirectory : TestFolder
	{
		public TempDirectory()
			: base("parsing")
		{
		}
	}
}