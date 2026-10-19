namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// Data of the <see cref="ISettingsService.SettingsChanged"/> event.
	/// </summary>
	public sealed class SettingsChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsChangedEventArgs"/> class.
		/// </summary>
		/// <param name="settings">The settings that are now in effect.</param>
		public SettingsChangedEventArgs(AppSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);
			this.Settings = settings;
		}

		/// <summary>Gets the settings that are now in effect.</summary>
		public AppSettings Settings { get; }
	}
}