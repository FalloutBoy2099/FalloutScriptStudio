namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The list of preprocessor definitions the user added to the choices of the build toolbar.
	/// </summary>
	/// <remarks>
	/// The legacy editor always offered <c>DEBUG</c> and <c>RELEASE</c> (items of the combo box itself) and appended the lines of
	/// <c>PreprocDefine.ini</c>; the "none" choice was a fixed first item, not a line of the file. <see cref="BuiltIn"/> keeps the two
	/// fixed definitions out of the stored list.
	/// </remarks>
	public sealed class PreprocessorDefines
	{
		/// <summary>The definitions that are always offered, before the ones of the user.</summary>
		public static readonly IReadOnlyList<String> BuiltIn = new[] { "DEBUG", "RELEASE" };

		/// <summary>Gets or sets the definitions added by the user.</summary>
		public List<String> Items { get; set; } = new List<String>();

		/// <summary>
		/// Tells whether a text can be a definition: it is not empty, has no white space and is not a placeholder such as
		/// <c>&lt;Not Preset&gt;</c> (the label of the "none" choice of the legacy editor).
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="true"/> when the text can be passed to the compiler as a definition.</returns>
		public static Boolean IsValidDefinition(String? text)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			String trimmed = text.Trim();
			return !IsPlaceholder(trimmed) && !trimmed.Any(Char.IsWhiteSpace);
		}

		/// <summary>
		/// Tells whether a text is a placeholder such as <c>&lt;Not Preset&gt;</c> (the label of the "none" choice of the legacy editor)
		/// instead of a definition.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="true"/> for a text written between angle brackets.</returns>
		public static Boolean IsPlaceholder(String? text)
		{
			String trimmed = (text ?? String.Empty).Trim();
			return trimmed.Length >= 2 && trimmed.StartsWith('<') && trimmed.EndsWith('>');
		}

		/// <summary>
		/// Gets all the definitions the toolbar offers: the built-in ones followed by the user's, without duplicates.
		/// </summary>
		/// <returns>The definitions in display order.</returns>
		public IReadOnlyList<String> GetChoices()
		{
			return BuiltIn.Concat(this.Items.Where(IsValidDefinition).Select(item => item.Trim())).Distinct(StringComparer.Ordinal).ToList();
		}

		/// <summary>
		/// Removes everything that is not a usable user definition: blank lines, placeholders (for example the label of the
		/// "none" choice), duplicates and the built-in definitions.
		/// </summary>
		public void Sanitize()
		{
			this.Items = (this.Items ?? new List<String>())
				.Where(IsValidDefinition)
				.Select(item => item.Trim())
				.Where(item => !BuiltIn.Contains(item, StringComparer.Ordinal))
				.Distinct(StringComparer.Ordinal)
				.ToList();
		}
	}
}
