namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using FalloutScriptStudio.Services;

	/// <summary>A sound service that counts the sounds that would have been played.</summary>
	internal sealed class FakeSoundService : ISoundService
	{
		public Int32 CompileFailedCount { get; private set; }

		public Int32 NotFoundCount { get; private set; }

		public void PlayCompileFailed()
		{
			this.CompileFailedCount++;
		}

		public void PlayNotFound()
		{
			this.NotFoundCount++;
		}
	}
}
