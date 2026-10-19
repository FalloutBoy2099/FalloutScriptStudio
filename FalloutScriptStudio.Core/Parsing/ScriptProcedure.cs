namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	/// <summary>
	/// A procedure of a script, together with its source locations.
	/// </summary>
	/// <remarks>
	/// All line numbers are 1-based (the first line of a file is line 1) and -1 means "unknown / not present".
	/// Instances are immutable; operations that change locations return modified copies.
	/// </remarks>
	public sealed class ScriptProcedure : IScriptSymbol
	{
		private static readonly HashSet<String> _standardHandlers = new(StringComparer.Ordinal)
		{
			"combat_is_over_p_proc",
			"combat_is_starting_p_proc",
			"combat_p_proc",
			"critter_p_proc",
			"damage_p_proc",
			"desc_p_proc",
			"description_p_proc",
			"destroy_p_proc",
			"drop_p_proc",
			"is_dropping_p_proc",
			"look_at_p_proc",
			"map_enter_p_proc",
			"map_exit_p_proc",
			"map_update_p_proc",
			"pickup_p_proc",
			"push_p_proc",
			"spatial_p_proc",
			"start",
			"talk_p_proc",
			"timed_event_p_proc",
			"use_obj_on_p_proc",
			"use_p_proc",
			"use_skill_on_p_proc",
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptProcedure"/> class.
		/// </summary>
		/// <param name="name">Procedure name as written in the script (case preserved).</param>
		public ScriptProcedure(String name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.NormalizedName = name.ToLowerInvariant();
		}

		/// <inheritdoc />
		public String Name { get; }

		/// <summary>Gets the lower-cased name used for case-insensitive lookups.</summary>
		public String NormalizedName { get; }

		/// <summary>Gets the modifiers of the procedure.</summary>
		public ProcedureFlags Flags { get; init; }

		/// <summary>Gets the full path of the file in which the procedure is declared (empty when unknown).</summary>
		public String DeclaredFile { get; init; } = String.Empty;

		/// <summary>Gets the full path of the file that contains the body of the procedure (empty when unknown).</summary>
		public String StartFile { get; init; } = String.Empty;

		/// <summary>Gets the lower-cased name of the file in which the procedure is declared.</summary>
		public String FileName { get; init; } = String.Empty;

		/// <summary>
		/// Gets the line of the declaration. This is the forward declaration (<c>procedure Foo;</c>) when the script has one,
		/// otherwise the line where the body starts (as reported by the native parser); -1 when neither is known.
		/// </summary>
		public Int32 DeclaredLine { get; init; } = -1;

		/// <summary>Gets the line of the definition (the same value as <see cref="StartLine"/>), or -1 when there is no body.</summary>
		public Int32 DefinedLine { get; init; } = -1;

		/// <summary>Gets the line where the body starts, or -1 when the procedure has no body in the script.</summary>
		public Int32 StartLine { get; init; } = -1;

		/// <summary>Gets the line of the closing <c>end</c>, or -1 when it was not found.</summary>
		public Int32 EndLine { get; init; } = -1;

		/// <summary>Gets the number of arguments (known only from the native parser).</summary>
		public Int32 ArgumentCount { get; init; }

		/// <summary>Gets the value of the <c>timed</c> modifier.</summary>
		public Int32 TimeValue { get; init; }

		/// <summary>Gets the variables local to the procedure; the first <see cref="ArgumentCount"/> items are its arguments.</summary>
		public IReadOnlyList<ScriptVariable> Variables { get; init; } = Array.Empty<ScriptVariable>();

		/// <inheritdoc />
		public IReadOnlyList<SourceReference> References { get; init; } = Array.Empty<SourceReference>();

		/// <inheritdoc />
		public SymbolKind Kind => SymbolKind.Procedure;

		/// <inheritdoc />
		public Boolean IsImported => (this.Flags & ProcedureFlags.Import) != ProcedureFlags.None;

		/// <inheritdoc />
		public Boolean IsExported => (this.Flags & ProcedureFlags.Export) != ProcedureFlags.None;

		/// <inheritdoc />
		public Boolean IsStandard => _standardHandlers.Contains(this.NormalizedName);

		/// <summary>Determines whether a name is one of the standard handlers the game engine calls in a script (<c>start</c>, <c>talk_p_proc</c>...).</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <returns><see langword="true"/> for a standard handler.</returns>
		public static Boolean IsStandardHandlerName(String name)
		{
			ArgumentNullException.ThrowIfNull(name);
			return _standardHandlers.Contains(name.ToLowerInvariant());
		}

		/// <summary>Gets a value indicating whether the script has a forward declaration that is distinct from the body.</summary>
		public Boolean HasForwardDeclaration => this.DeclaredLine > 0 && this.DeclaredLine != this.StartLine;

		/// <summary>Gets the first line occupied by the procedure (forward declaration or body), used to order procedures.</summary>
		public Int32 SortLine => this.DeclaredLine > 0 ? this.DeclaredLine : this.StartLine;

		/// <inheritdoc />
		public SourceReference? Declaration => new SourceReference(this.DeclaredFile, this.DeclaredLine);

		/// <inheritdoc />
		public String FullDescription => this.Describe(true);

		/// <inheritdoc />
		public String ShortDescription => this.Describe(false);

		/// <summary>Determines whether the body of the procedure lies in the given script file.</summary>
		/// <param name="scriptFile">Full path of the script.</param>
		/// <returns><c>true</c> when the body is in that file.</returns>
		public Boolean IsDefinedIn(String? scriptFile)
		{
			return String.Equals(this.StartFile, scriptFile ?? String.Empty, StringComparison.OrdinalIgnoreCase);
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return this.FullDescription;
		}

		/// <summary>Returns a copy with the given source locations.</summary>
		internal ScriptProcedure WithLocations(Int32 declaredLine, Int32 definedLine, Int32 startLine, Int32 endLine)
		{
			return this.CopyWith(this.Name, declaredLine, definedLine, startLine, endLine);
		}

		/// <summary>Returns a copy with another name (used to resolve duplicated names).</summary>
		internal ScriptProcedure WithName(String name)
		{
			return this.CopyWith(name, this.DeclaredLine, this.DefinedLine, this.StartLine, this.EndLine);
		}

		/// <summary>Returns a copy bound to another script file.</summary>
		internal ScriptProcedure WithFile(String filePath, IReadOnlyList<SourceReference> references, IReadOnlyList<ScriptVariable> variables)
		{
			return new ScriptProcedure(this.Name)
			{
				Flags = this.Flags,
				DeclaredFile = filePath,
				StartFile = filePath,
				FileName = Path.GetFileName(filePath).ToLowerInvariant(),
				DeclaredLine = this.DeclaredLine,
				DefinedLine = this.DefinedLine,
				StartLine = this.StartLine,
				EndLine = this.EndLine,
				ArgumentCount = this.ArgumentCount,
				TimeValue = this.TimeValue,
				Variables = variables,
				References = references,
			};
		}

		private static void AppendIf(StringBuilder sb, ProcedureFlags flags, ProcedureFlags flag, String text)
		{
			if ((flags & flag) != ProcedureFlags.None)
			{
				sb.Append(text);
			}
		}

		private ScriptProcedure CopyWith(String name, Int32 declaredLine, Int32 definedLine, Int32 startLine, Int32 endLine)
		{
			return new ScriptProcedure(name)
			{
				Flags = this.Flags,
				DeclaredFile = this.DeclaredFile,
				StartFile = this.StartFile,
				FileName = this.FileName,
				DeclaredLine = declaredLine,
				DefinedLine = definedLine,
				StartLine = startLine,
				EndLine = endLine,
				ArgumentCount = this.ArgumentCount,
				TimeValue = this.TimeValue,
				Variables = this.Variables,
				References = this.References,
			};
		}

		private String Describe(Boolean fullSpec)
		{
			var sb = new StringBuilder();
			if (fullSpec)
			{
				AppendIf(sb, this.Flags, ProcedureFlags.Import, "imported ");
				AppendIf(sb, this.Flags, ProcedureFlags.Export, "exported ");
				AppendIf(sb, this.Flags, ProcedureFlags.Pure, "pure ");
				AppendIf(sb, this.Flags, ProcedureFlags.Inline, "inline ");
				AppendIf(sb, this.Flags, ProcedureFlags.Timed, "timed ");
				AppendIf(sb, this.Flags, ProcedureFlags.Conditional, "conditional ");
				AppendIf(sb, this.Flags, ProcedureFlags.Critical, "critical ");
				sb.Append("procedure ");
			}

			sb.Append(this.Name).Append(" (");
			for (Int32 i = 0; i < this.ArgumentCount; i++)
			{
				if (i > 0)
				{
					sb.Append(", ");
				}

				sb.Append(i >= this.Variables.Count ? "x" : this.Variables[i].Name);
				if (fullSpec && i < this.Variables.Count && this.Variables[i].InitialValue is not null)
				{
					sb.Append(" := ").Append(this.Variables[i].InitialValue);
				}
			}

			sb.Append(')');
			return sb.ToString();
		}
	}
}