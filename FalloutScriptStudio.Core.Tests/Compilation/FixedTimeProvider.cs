namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;

	/// <summary>
	/// A clock that always shows 12:00:00 UTC.
	/// </summary>
	internal sealed class FixedTimeProvider : TimeProvider
	{
		public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

		public override DateTimeOffset GetUtcNow()
		{
			return new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
		}
	}
}
