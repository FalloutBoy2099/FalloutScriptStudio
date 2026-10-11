namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;

	/// <summary>Writes the path of a header the way it appears in an <c>#include</c> line.</summary>
	public static class IncludePathFormatter
	{
		/// <summary>
		/// Gets the text of the include path: relative to the headers folder when the header is inside it (with forward slashes),
		/// otherwise the full path.
		/// </summary>
		/// <param name="headerPath">The full path of the header.</param>
		/// <param name="headersDirectory">The headers folder of the settings, or <see langword="null"/>.</param>
		public static String Format(String headerPath, String? headersDirectory)
		{
			ArgumentException.ThrowIfNullOrEmpty(headerPath);
			if (!String.IsNullOrEmpty(headersDirectory))
			{
				String root = Path.GetFullPath(headersDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
				String full = Path.GetFullPath(headerPath);
				if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
				{
					return full.Substring(root.Length).Replace('\\', '/');
				}
			}

			return headerPath;
		}
	}
}
