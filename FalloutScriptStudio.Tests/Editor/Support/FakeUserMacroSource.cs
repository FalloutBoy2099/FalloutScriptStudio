namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Editor;

	/// <summary>A user macro list that tests can change.</summary>
	public sealed class FakeUserMacroSource : IUserMacroSource
	{
		private IReadOnlyCollection<String> _words = Array.Empty<String>();

		public event EventHandler? Changed;

		public IReadOnlyCollection<String> GetWords()
		{
			return this._words;
		}

		public void Set(params String[] words)
		{
			this._words = words;
			this.Changed?.Invoke(this, EventArgs.Empty);
		}
	}
}
