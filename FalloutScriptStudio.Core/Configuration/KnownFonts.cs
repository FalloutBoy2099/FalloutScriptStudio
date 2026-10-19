namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// Facts about the editor fonts that ship with the program.
	/// </summary>
	public static class KnownFonts
	{
		/// <summary>The font used when no custom font is selected.</summary>
		public const String DefaultFamily = "Courier New";

		/// <summary>The point size of the default font before the user adjustment.</summary>
		public const Double DefaultSize = 10.0;

		/// <summary>
		/// Gets the base point size of a font family, chosen so that all fonts look equally big.
		/// </summary>
		/// <param name="family">The family name.</param>
		/// <returns>The base size in points.</returns>
		public static Double GetBaseSize(String family)
		{
			return family switch
			{
				"Anonymous Pro" => 10.5,
				"Consolas" => 10.5,
				"Cousine" => 10.5,
				"InconsolataCyr" => 11.0,
				"InputMono" => 9.5,
				"InputMonoCondensed" => 9.5,
				"Liberation Mono" => 10.25,
				"Meslo LG S DZ" => 9.75,
				"Ubuntu Mono" => 11.75,
				_ => DefaultSize,
			};
		}
	}
}