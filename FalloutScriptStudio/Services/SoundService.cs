namespace FalloutScriptStudio.Services
{
	using System;
	using System.IO;
	using System.Media;
	using System.Windows;
	using System.Windows.Resources;

	/// <inheritdoc />
	public sealed class SoundService : ISoundService
	{
		private const String CompileErrorUri = "pack://application:,,,/Assets/Sounds/CompileError.wav";
		private const String NotFoundUri = "pack://application:,,,/Assets/Sounds/DontFind.wav";

		public void PlayCompileFailed()
		{
			Play(CompileErrorUri);
		}

		public void PlayNotFound()
		{
			Play(NotFoundUri);
		}

		private static void Play(String resourceUri)
		{
			try
			{
				StreamResourceInfo? info = Application.GetResourceStream(new Uri(resourceUri));
				if (info is null)
				{
					SystemSounds.Beep.Play();
					return;
				}

				SoundPlayer player = new SoundPlayer(info.Stream);
				player.Load();
				player.Play();
			}
			catch (Exception ex) when (ex is IOException or InvalidOperationException or UriFormatException)
			{
				SystemSounds.Beep.Play();
			}
		}
	}
}
