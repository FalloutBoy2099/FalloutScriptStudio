namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>How serious a <see cref="DialogueQualityFinding"/> is.</summary>
	public enum DialogueQualitySeverity
	{
		/// <summary>The dialogue cannot work (the game or the compiler rejects it).</summary>
		Error = 0,

		/// <summary>Probably a mistake of the dialogue.</summary>
		Warning,

		/// <summary>A note.</summary>
		Info,
	}
}