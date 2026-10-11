namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using Xunit;

	/// <summary>
	/// A fact that needs the real sfall tools. It is reported as skipped (not failed) when they are absent, see <see cref="SslcTools"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class RealToolFactAttribute : FactAttribute
	{
		public RealToolFactAttribute(RealTools tools)
		{
			this.Skip = SslcTools.GetSkipReason(tools);
		}
	}
}
