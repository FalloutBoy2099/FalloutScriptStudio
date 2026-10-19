namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;

	/// <summary>Path helpers shared by the workspace classes.</summary>
	internal static class DocumentPaths
	{
		/// <summary>Returns the absolute form of a path, or the path itself when it cannot be resolved.</summary>
		public static String GetFullPath(String path)
		{
			try
			{
				return Path.GetFullPath(path);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path;
			}
		}
	}
}