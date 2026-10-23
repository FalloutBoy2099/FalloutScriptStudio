namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using Xunit;

	/// <summary>A theory that drives the published executables: skipped as the other smoke tests, and also when the publish folder is missing or older than the sources (see <see cref="UiSmokeEnvironment.PublishedSkipReason"/>).</summary>
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class UiSmokePublishedTheoryAttribute : TheoryAttribute
	{
		/// <summary>Initializes a new instance of the <see cref="UiSmokePublishedTheoryAttribute"/> class.</summary>
		public UiSmokePublishedTheoryAttribute()
		{
			this.Skip = UiSmokeEnvironment.PublishedSkipReason;
		}
	}
}