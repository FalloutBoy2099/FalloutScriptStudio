namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.IO;

	internal static class FlowchartViewModelTestHelpers
	{
		internal static String FcdPath(FlowchartHarness harness)
		{
			return Path.ChangeExtension(harness.Script.FilePath!, ".fcd");
		}
	}
}