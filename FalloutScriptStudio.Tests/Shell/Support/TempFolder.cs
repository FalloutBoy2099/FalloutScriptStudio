namespace FalloutScriptStudio.Tests.Shell.Support
{
	using FalloutScriptStudio.TestSupport;

	/// <summary>A uniquely named scratch folder in <c>_test_files\work</c> that is deleted when disposed.</summary>
	internal sealed class TempFolder : TestFolder
	{
		public TempFolder()
			: base("shell")
		{
		}
	}
}
