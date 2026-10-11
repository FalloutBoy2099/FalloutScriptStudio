namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	/// <summary>What is wrong with the files needed for the script registration.</summary>
	public enum ScriptRegistryProblemKind
	{
		/// <summary>The scripts folder is not configured.</summary>
		OutputDirectoryNotSet,

		/// <summary><c>scripts.lst</c> does not exist.</summary>
		ScriptsListMissing,

		/// <summary><c>scrname.msg</c> does not exist.</summary>
		NameMessageMissing,

		/// <summary>The <c>scripts.h</c> path is not configured.</summary>
		HeaderNotSet,

		/// <summary>The configured <c>scripts.h</c> does not exist.</summary>
		HeaderMissing,
	}
}
