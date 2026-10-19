namespace FalloutScriptStudio.Workspace
{
	using System;
	using Core.Configuration;
	using Core.Parsing;

	/// <summary>Remembers the folded procedures of every script in the persisted <see cref="FoldedProcedures"/> model.</summary>
	public sealed class SettingsCollapsedProcedureStore : ICollapsedProcedureStore
	{
		private readonly ISettingsService _settings;

		/// <summary>Initializes the store.</summary>
		public SettingsCollapsedProcedureStore(ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(settings);
			this._settings = settings;
		}

		public Boolean IsCollapsed(String scriptName, String procedureName)
		{
			return this._settings.FoldedProcedures.IsFolded(scriptName, procedureName);
		}

		public void SetCollapsed(String scriptName, String procedureName, Boolean collapsed)
		{
			if (collapsed)
			{
				this._settings.FoldedProcedures.SetFolded(scriptName, procedureName);
			}
			else
			{
				this._settings.FoldedProcedures.SetUnfolded(scriptName, procedureName);
			}
		}
	}
}