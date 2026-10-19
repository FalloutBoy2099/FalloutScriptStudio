namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using Xunit;

	/// <summary>
	/// The tests that change the language of the interface. The change is announced to every live view model of the process, so they must
	/// not run while other tests use their view models on other threads.
	/// </summary>
	[CollectionDefinition(Name, DisableParallelization = true)]
	public sealed class LanguageCollection
	{
		/// <summary>The name of the collection.</summary>
		public const String Name = "Language";

		/// <summary>Collects the view models of finished tests, whose weak language subscriptions then end.</summary>
		public static void CollectGarbage()
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
	}
}