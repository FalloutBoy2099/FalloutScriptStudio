namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using Xunit;

	/// <summary>A theory that drives the real program: reported as skipped unless <c>FSS_RUN_UI_TESTS=1</c> is set (see <see cref="UiSmokeEnvironment"/>).</summary>
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class UiSmokeTheoryAttribute : TheoryAttribute
	{
		/// <summary>Initializes a new instance of the <see cref="UiSmokeTheoryAttribute"/> class.</summary>
		public UiSmokeTheoryAttribute()
		{
			this.Skip = UiSmokeEnvironment.SkipReason;
		}
	}
}