namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// Shares one set of generated inputs between all the <c>Performance</c> tests of the assembly. The collection does not run in parallel with the other tests (they start
	/// after the others ended): a time budget measured while another test uses the processors is a measure of that test, not of the code.
	/// </summary>
	[CollectionDefinition(Name, DisableParallelization = true)]
	public sealed class PerformanceCollection : ICollectionFixture<PerformanceInputs>
	{
		/// <summary>The name of the collection.</summary>
		public const String Name = "Performance";
	}
}