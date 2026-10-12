namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Xunit;

	/// <summary>Asserts that a scan found nothing, with the whole list of findings in the failure message.</summary>
	internal static class Findings
	{
		public static void AssertNone(IEnumerable<String> findings)
		{
			String[] list = findings.ToArray();
			Assert.True(list.Length == 0, Environment.NewLine + String.Join(Environment.NewLine, list));
		}
	}
}
