namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	/// <summary>The outcome of adding a script definition to <c>scripts.h</c>.</summary>
	public enum DefineInsertResult
	{
		/// <summary>The definition was added.</summary>
		Added,

		/// <summary>A definition with that name already exists; nothing was changed.</summary>
		AlreadyDefined,
	}
}
