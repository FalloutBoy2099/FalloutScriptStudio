namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Threading.Tasks;

	/// <summary>Waits for a condition that becomes true on a background thread.</summary>
	internal static class Eventually
	{
		public static async Task<Boolean> TrueAsync(Func<Boolean> condition, Int32 timeoutMilliseconds = 120000)
		{
			DateTime limit = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
			while (DateTime.UtcNow < limit)
			{
				if (condition())
				{
					return true;
				}

				await Task.Delay(10);
			}

			return condition();
		}
	}
}