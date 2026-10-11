namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	/// <summary>How a script may be registered.</summary>
	public enum ScriptRegistrationCheckKind
	{
		/// <summary>The script can be registered.</summary>
		Allowed,

		/// <summary>The script cannot be registered; the message tells why.</summary>
		Refused,

		/// <summary>The script can be registered if the user confirms the warning.</summary>
		NeedsConfirmation,
	}
}
