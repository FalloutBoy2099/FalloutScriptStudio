namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Services.ToolWindows;

	internal sealed class FakeFontCatalog : IEditorFontCatalog
	{
		public FakeFontCatalog(params String[] names)
		{
			this.Names = names;
		}

		public IReadOnlyList<String> Names { get; }

		public IReadOnlyList<String> GetFamilyNames(String fontsDirectory)
		{
			return this.Names;
		}
	}
}
