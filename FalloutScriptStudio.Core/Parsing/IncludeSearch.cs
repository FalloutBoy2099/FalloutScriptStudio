namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// The single rule that decides whether the headers folder takes part in <c>#include</c> resolution
	/// (legacy <c>Settings.IsSearchIncludes</c>: the option is checked and a <c>scripts.h</c> file is configured).
	/// </summary>
	public static class IncludeSearch
	{
		/// <summary>Tells whether the headers folder is searched for includes.</summary>
		/// <param name="searchIncludePath">The "search includes" option.</param>
		/// <param name="scriptsHeaderFile">The configured <c>scripts.h</c> path, if any.</param>
		/// <returns><see langword="true"/> when the headers folder is searched.</returns>
		public static Boolean IsEnabled(Boolean searchIncludePath, String? scriptsHeaderFile)
		{
			return searchIncludePath && !String.IsNullOrWhiteSpace(scriptsHeaderFile);
		}

		/// <summary>Gets the headers folder to hand to the parsers, the compiler and the include resolver.</summary>
		/// <param name="searchIncludePath">The "search includes" option.</param>
		/// <param name="scriptsHeaderFile">The configured <c>scripts.h</c> path, if any.</param>
		/// <param name="headersDirectory">The configured headers folder, if any.</param>
		/// <returns>The folder, or <see langword="null"/> when the include search is disabled or no folder is configured.</returns>
		public static String? GetHeadersDirectory(Boolean searchIncludePath, String? scriptsHeaderFile, String? headersDirectory)
		{
			return IsEnabled(searchIncludePath, scriptsHeaderFile) && !String.IsNullOrWhiteSpace(headersDirectory) ? headersDirectory : null;
		}
	}
}