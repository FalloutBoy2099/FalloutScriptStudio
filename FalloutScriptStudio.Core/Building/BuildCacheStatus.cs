namespace FalloutScriptStudio.Core.Building
{
	/// <summary>How the build cache file was found when it was loaded.</summary>
	public enum BuildCacheStatus
	{
		/// <summary>The file was read.</summary>
		Loaded = 0,

		/// <summary>There is no file yet.</summary>
		Missing = 1,

		/// <summary>The file exists but its content is not a valid cache of this version; it is ignored and replaced at the next save.</summary>
		Corrupt = 2,

		/// <summary>The file exists but could not be read (a program holds it); the cache starts empty and the file is not overwritten before it can be read.</summary>
		Unreadable = 3,
	}
}