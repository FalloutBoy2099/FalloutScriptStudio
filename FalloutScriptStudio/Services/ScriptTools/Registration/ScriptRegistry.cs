namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// The content of <c>scripts.lst</c> (with the names from <c>scrname.msg</c>) while it is edited.
	/// </summary>
	public sealed class ScriptRegistry
	{
		/// <summary>Initializes a new instance of the <see cref="ScriptRegistry"/> class.</summary>
		/// <param name="listEncoding">The encoding <c>scripts.lst</c> is written back with.</param>
		public ScriptRegistry(Encoding listEncoding)
		{
			this.ListEncoding = listEncoding;
		}

		/// <summary>Gets the entries in list order; the position is the script id.</summary>
		public List<ScriptListEntry> Entries { get; } = [];

		/// <summary>Gets the encoding <c>scripts.lst</c> is written back with.</summary>
		public Encoding ListEncoding { get; }
	}
}