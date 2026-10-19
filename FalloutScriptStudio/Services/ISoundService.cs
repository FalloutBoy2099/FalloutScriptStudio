namespace FalloutScriptStudio.Services
{
	/// <summary>Plays the short feedback sounds of the editor.</summary>
	public interface ISoundService
	{
		/// <summary>Plays the sound of a failed compilation.</summary>
		void PlayCompileFailed();

		/// <summary>Plays the sound of a search that found nothing.</summary>
		void PlayNotFound();
	}
}