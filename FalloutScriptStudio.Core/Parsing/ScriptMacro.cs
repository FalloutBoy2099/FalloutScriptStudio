namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	/// <summary>
	/// A <c>#define</c> macro collected from a script or a header file.
	/// </summary>
	public sealed class ScriptMacro : IScriptSymbol
	{
		private readonly Lazy<String> _fullDescription;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptMacro"/> class.
		/// </summary>
		/// <param name="token">Macro name (the lookup key), case preserved.</param>
		/// <param name="definitionName">Macro name with its parameter list, for example <c>MAX(a, b)</c>.</param>
		/// <param name="code">Macro body.</param>
		/// <param name="declaredFile">File where the macro is defined.</param>
		/// <param name="declaredLine">1-based line of the <c>#define</c>.</param>
		/// <param name="description">Documentation comment (<c>/** ... **/</c>) preceding the macro, if any.</param>
		public ScriptMacro(String token, String definitionName, String code, String declaredFile, Int32 declaredLine, String? description)
		{
			this.Token = token;
			this.DefinitionName = definitionName;
			this.Code = code;
			this.DeclaredFile = declaredFile;
			this.DeclaredLine = declaredLine;
			this.Description = description;
			this._fullDescription = new Lazy<String>(this.BuildFullDescription);
		}

		/// <summary>Gets the macro name used as a lookup key.</summary>
		public String Token { get; }

		/// <summary>Gets the macro name together with its parameters.</summary>
		public String DefinitionName { get; }

		/// <summary>Gets the macro body.</summary>
		public String Code { get; }

		/// <summary>Gets the file in which the macro is defined (empty when the script is unsaved).</summary>
		public String DeclaredFile { get; }

		/// <summary>Gets the 1-based line of the definition.</summary>
		public Int32 DeclaredLine { get; }

		/// <summary>Gets the documentation comment, or <c>null</c>.</summary>
		public String? Description { get; }

		/// <inheritdoc />
		public String Name => this.Token;

		/// <inheritdoc />
		public SymbolKind Kind => SymbolKind.Macro;

		/// <inheritdoc />
		public Boolean IsImported => false;

		/// <inheritdoc />
		public Boolean IsExported => false;

		/// <inheritdoc />
		public Boolean IsStandard => false;

		/// <inheritdoc />
		public IReadOnlyList<SourceReference> References => Array.Empty<SourceReference>();

		/// <inheritdoc />
		public SourceReference? Declaration => new SourceReference(this.DeclaredFile, this.DeclaredLine);

		/// <inheritdoc />
		public String FullDescription => this._fullDescription.Value;

		/// <inheritdoc />
		public String ShortDescription => "Define: " + this.DefinitionName;

		/// <inheritdoc />
		public override String ToString()
		{
			return this.FullDescription;
		}

		private String BuildFullDescription()
		{
			String declare = String.IsNullOrEmpty(this.DeclaredFile)
				? String.Empty
				: "\n\nDeclare file: " + Path.GetFileName(this.DeclaredFile);

			String desc = this.Description is null ? "\n" : "\n - " + this.Description.TrimEnd() + "\n\n";
			return "Define: " + this.DefinitionName + desc + this.Code + declare;
		}
	}
}
