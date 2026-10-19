namespace FalloutScriptStudio.Commands
{
	using System;
	using Localization;

	/// <summary>A piece of the name of a command: a localization key, which follows the language, or a text that is not translated (a file, a profile).</summary>
	public readonly record struct CommandText
	{
		private CommandText(String value, Boolean isKey)
		{
			this.Value = value;
			this.IsKey = isKey;
		}

		/// <summary>Gets the localization key, or the literal text.</summary>
		public String Value { get; }

		/// <summary>Gets a value indicating whether <see cref="Value"/> is a localization key.</summary>
		public Boolean IsKey { get; }

		/// <summary>Creates a piece that is looked up in the localized texts (the menu header the key stands for).</summary>
		/// <param name="key">The localization key.</param>
		/// <returns>The piece.</returns>
		public static CommandText FromKey(String key)
		{
			ArgumentException.ThrowIfNullOrEmpty(key);
			return new CommandText(key, true);
		}

		/// <summary>Creates a piece that is shown as it is.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The piece.</returns>
		public static CommandText FromLiteral(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			return new CommandText(text, false);
		}

		/// <summary>Gets the text in the current language, without access key marker and ellipsis.</summary>
		/// <returns>The plain text.</returns>
		public String Resolve()
		{
			return this.IsKey ? MenuText.Clean(Loc.Get(this.Value)) : this.Value;
		}
	}
}