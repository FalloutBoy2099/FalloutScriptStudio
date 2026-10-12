namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>Shares one set of generated inputs between all the <c>Performance</c> tests of the assembly.</summary>
	[CollectionDefinition(Name)]
	public sealed class PerformanceCollection : ICollectionFixture<PerformanceInputs>
	{
		/// <summary>The name of the collection.</summary>
		public const String Name = "Performance";
	}
}
