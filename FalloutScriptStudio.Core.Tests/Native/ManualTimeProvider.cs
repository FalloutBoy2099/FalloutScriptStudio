namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;

	/// <summary>A clock that only moves when the test advances it.</summary>
	internal sealed class ManualTimeProvider : TimeProvider
	{
		private DateTimeOffset _now = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow()
		{
			return this._now;
		}

		public void Advance(TimeSpan span)
		{
			this._now += span;
		}
	}
}
