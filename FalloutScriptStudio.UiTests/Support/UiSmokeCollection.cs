namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using Xunit;

	/// <summary>The collection of the real-UI tests: they run one after the other (one program at a time), and share <see cref="UiSmokeSuite"/>.</summary>
	[CollectionDefinition(Name)]
	public sealed class UiSmokeCollection : ICollectionFixture<UiSmokeSuite>
	{
		/// <summary>The name of the collection.</summary>
		public const String Name = "UiSmoke";
	}
}