namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Localization;
	using Wpf.Ui.Controls;

	/// <summary>A symbol in the symbol list: what to show for it (the texts follow a language change).</summary>
	public sealed class SymbolListItem
	{
		private readonly String? _rootFolder;

		/// <summary>Initializes a new instance of the <see cref="SymbolListItem"/> class.</summary>
		/// <param name="symbol">The symbol.</param>
		/// <param name="rootFolder">The folder that the path is shown relative to, or <see langword="null"/>.</param>
		public SymbolListItem(IndexedSymbol symbol, String? rootFolder)
		{
			ArgumentNullException.ThrowIfNull(symbol);
			this.Symbol = symbol;
			this._rootFolder = rootFolder;
		}

		/// <summary>Gets the symbol.</summary>
		public IndexedSymbol Symbol { get; }

		/// <summary>Gets the name to show: the name, or the macro with its parameters.</summary>
		public String DisplayName => this.Symbol.DisplayName;

		/// <summary>Gets the icon of the kind of the symbol.</summary>
		public SymbolRegular Icon => this.Symbol.Kind switch
		{
			IndexedSymbolKind.Procedure => SymbolRegular.Code24,
			IndexedSymbolKind.Macro => SymbolRegular.Tag24,
			_ => SymbolRegular.BracesVariable24,
		};

		/// <summary>Gets the name of the kind in the current language.</summary>
		public String KindText => this.Symbol.Kind switch
		{
			IndexedSymbolKind.Procedure => Loc.Get(this.Symbol.HasBody ? "Str.ToolWindows.Symbols.KindProcedure" : "Str.ToolWindows.Symbols.KindProcedureDeclaration"),
			IndexedSymbolKind.Macro => Loc.Get("Str.ToolWindows.Symbols.KindMacro"),
			_ => Loc.Get(this.Symbol.Scope switch
			{
				VariableScope.Import => "Str.ToolWindows.Symbols.KindImportedVariable",
				VariableScope.Export => "Str.ToolWindows.Symbols.KindExportedVariable",
				_ => "Str.ToolWindows.Symbols.KindVariable",
			}),
		};

		/// <summary>Gets the path of the file, relative to the root folder when it is below it.</summary>
		public String FileText
		{
			get
			{
				if (!String.IsNullOrEmpty(this._rootFolder))
				{
					String relative = Path.GetRelativePath(this._rootFolder, this.Symbol.File);
					if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
					{
						return relative;
					}
				}

				return this.Symbol.File;
			}
		}

		/// <summary>Gets the file and the line in the current language.</summary>
		public String LocationText => Loc.Format("Str.ToolWindows.Symbols.Location", this.FileText, this.Symbol.Line);

		/// <summary>Gets the text a screen reader reads for the item: the kind, the name, the file and the line.</summary>
		public String AccessibleName => Loc.Format("Str.ToolWindows.Symbols.ItemName", this.KindText, this.DisplayName, this.FileText, this.Symbol.Line);
	}
}
