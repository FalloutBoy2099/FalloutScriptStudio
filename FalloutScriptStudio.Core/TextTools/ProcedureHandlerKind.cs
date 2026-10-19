namespace FalloutScriptStudio.Core.TextTools
{
	/// <summary>
	/// Kind of an entry of the procedure handler menu.
	/// </summary>
	public enum ProcedureHandlerKind
	{
		/// <summary>A procedure that can be created.</summary>
		Handler,

		/// <summary>A sub menu holding other entries.</summary>
		Group,

		/// <summary>A separator line.</summary>
		Separator,
	}
}