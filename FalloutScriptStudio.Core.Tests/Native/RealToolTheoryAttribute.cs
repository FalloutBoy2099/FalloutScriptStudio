namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using Xunit;

	/// <summary>
	/// A theory that needs the real sfall tools. It is reported as skipped (not failed) when they are absent, see <see cref="SslcTools"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class RealToolTheoryAttribute : TheoryAttribute
	{
		public RealToolTheoryAttribute(RealTools tools)
		{
			this.Skip = SslcTools.GetSkipReason(tools);
		}
	}
}
