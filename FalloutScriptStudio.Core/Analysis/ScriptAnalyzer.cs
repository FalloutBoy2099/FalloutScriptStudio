namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Parsing;

	/// <summary>
	/// One run of the semantic analysis of a script (see <see cref="ScriptDiagnostics"/>). The run reads the tokens three times: a pass
	/// over the preprocessor lines (macros, includes, conditional blocks), an outline of the declarations and of the begin / end
	/// structure, and a pass over the bodies that resolves every name. Whatever the analysis cannot be sure of (a macro that may declare
	/// names, a header that was not read, a block that is compiled only under a condition) makes it stay quiet or lowers the severity:
	/// a script that the compiler accepts must never be reported with an error.
	/// </summary>
	internal sealed partial class ScriptAnalyzer
	{
		private readonly SslTokenStream _tokens;
		private readonly Int32 _count;
		private readonly ScriptEnvironment _environment;
		private readonly ScriptDiagnosticOptions _options;
		private readonly CancellationToken _cancellation;
		private readonly List<ScriptDiagnostic> _diagnostics = [];

		private readonly List<ConditionFrame> _frames = [];
		private readonly Dictionary<String, MacroTraits> _localMacros = new(StringComparer.Ordinal);
		private readonly Dictionary<String, MacroTraits?> _macroCache = new(StringComparer.Ordinal);
		private readonly Dictionary<String, MacroTraits>.AlternateLookup<ReadOnlySpan<Char>> _localMacroLookup;
		private readonly Dictionary<String, MacroTraits?>.AlternateLookup<ReadOnlySpan<Char>> _macroCacheLookup;
		private readonly List<IncludeDirective> _includes = [];

		private readonly List<ProcedureDeclaration> _procedures = [];
		private readonly Dictionary<String, List<ProcedureDeclaration>> _proceduresByName = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<String, List<VariableDeclarationInfo>> _globals = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<String, ProcedureDeclaration> _localOwners = new(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<String> _maybeProcedures = new(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<String> _maybeVariables = new(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<String> _referencedProcedures = new(StringComparer.OrdinalIgnoreCase);
		private readonly List<PendingDiagnostic> _structure = [];
		private readonly Stack<Int32> _blocks = new Stack<Int32>();
		private readonly Dictionary<String, List<ProcedureDeclaration>>.AlternateLookup<ReadOnlySpan<Char>> _procedureLookup;
		private readonly Dictionary<String, List<VariableDeclarationInfo>>.AlternateLookup<ReadOnlySpan<Char>> _globalLookup;
		private readonly Dictionary<String, ProcedureDeclaration>.AlternateLookup<ReadOnlySpan<Char>> _ownerLookup;
		private readonly HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> _maybeProcedureLookup;
		private readonly HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> _maybeVariableLookup;

		private readonly Dictionary<String, String?> _suggestions = new(StringComparer.OrdinalIgnoreCase);

		private Int32[] _frameOf = Array.Empty<Int32>();
		private Boolean[] _declaresName = Array.Empty<Boolean>();
		private ProcedureDeclaration? _current;
		private Int32 _parenDepth;
		private Int32 _globalTaintFrom = -1;
		private Int32 _firstIncludeToken = Int32.MaxValue;
		private Boolean _structuralMacroUsed;

		public ScriptAnalyzer(SslTokenStream tokens, ScriptEnvironment environment, ScriptDiagnosticOptions options, CancellationToken cancellation)
		{
			this._tokens = tokens;
			this._count = tokens.Count;
			this._environment = environment;
			this._options = options;
			this._cancellation = cancellation;
			this._localMacroLookup = this._localMacros.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._macroCacheLookup = this._macroCache.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._procedureLookup = this._proceduresByName.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._globalLookup = this._globals.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._ownerLookup = this._localOwners.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._maybeProcedureLookup = this._maybeProcedures.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._maybeVariableLookup = this._maybeVariables.GetAlternateLookup<ReadOnlySpan<Char>>();
		}

		/// <summary>Runs the analysis.</summary>
		/// <returns>The diagnostics in text order.</returns>
		public IReadOnlyList<ScriptDiagnostic> Analyze()
		{
			this.ScanDirectives();
			this.Outline();
			this.CheckBodies();
			this.Finish();
			this._diagnostics.Sort(CompareDiagnostics);
			return this._diagnostics;
		}

		/// <summary>Reads the declarations of the text without checking anything.</summary>
		/// <returns>The table of the symbols.</returns>
		public ScriptSymbolTable ExtractSymbols()
		{
			this.ScanDirectives();
			this.Outline();

			Dictionary<String, ScriptProcedureSymbol> procedures = new(StringComparer.OrdinalIgnoreCase);
			foreach (ProcedureDeclaration declaration in this._procedures)
			{
				Boolean body = declaration.Kind == ProcedureKind.Definition;
				if (procedures.TryGetValue(declaration.Name, out ScriptProcedureSymbol? known))
				{
					procedures[declaration.Name] = new ScriptProcedureSymbol(known.Name, Math.Min(known.MinArguments, declaration.MinArguments), Math.Max(known.MaxArguments, declaration.MaxArguments), known.HasBody || body);
				}
				else
				{
					procedures[declaration.Name] = new ScriptProcedureSymbol(declaration.Name, declaration.MinArguments, declaration.MaxArguments, body);
				}
			}

			foreach (String name in this._maybeProcedures)
			{
				if (!procedures.ContainsKey(name))
				{
					procedures[name] = new ScriptProcedureSymbol(name, 0, Int32.MaxValue, true);
				}
			}

			List<String> variables = new(this._globals.Count + this._maybeVariables.Count);
			foreach (KeyValuePair<String, List<VariableDeclarationInfo>> entry in this._globals)
			{
				variables.Add(entry.Value[0].Name);
			}

			foreach (String name in this._maybeVariables)
			{
				if (!this._globals.ContainsKey(name))
				{
					variables.Add(name);
				}
			}

			List<String> includes = new(this._includes.Count);
			foreach (IncludeDirective include in this._includes)
			{
				includes.Add(include.Path);
			}

			return new ScriptSymbolTable(new List<ScriptProcedureSymbol>(procedures.Values), variables, includes, this._globalTaintFrom >= 0);
		}

		/// <summary>Lists the paths of the <c>#include</c> directives of a text, as written, in order.</summary>
		/// <param name="tokens">The tokens of the text.</param>
		/// <returns>The paths.</returns>
		public static IReadOnlyList<String> FindIncludes(SslTokenStream tokens)
		{
			List<String> paths = [];
			for (Int32 i = 0; i < tokens.Count; i++)
			{
				if (tokens[i].Kind == SslTokenKind.Directive && tokens.GetDirectiveName(i) == "include")
				{
					Int32 end = FindDirectiveEnd(tokens, i);
					String? path = ReadIncludePath(tokens, i, end);
					if (path is not null)
					{
						paths.Add(path);
					}

					i = end;
				}
			}

			return paths;
		}

		private static Int32 CompareDiagnostics(ScriptDiagnostic a, ScriptDiagnostic b)
		{
			Int32 byOffset = a.Offset.CompareTo(b.Offset);
			return byOffset != 0 ? byOffset : String.CompareOrdinal(a.Code, b.Code);
		}

		private static Int32 FindDirectiveEnd(SslTokenStream tokens, Int32 directive)
		{
			Int32 end = directive;
			while (end + 1 < tokens.Count && tokens[end + 1].InDirective && tokens[end + 1].Kind != SslTokenKind.Directive)
			{
				end++;
			}

			return end;
		}

		private static String? ReadIncludePath(SslTokenStream tokens, Int32 directive, Int32 end)
		{
			Int32 next = directive + 1;
			if (next > end)
			{
				return null;
			}

			if (tokens[next].Kind == SslTokenKind.String)
			{
				String quoted = tokens.GetText(next);
				return quoted.Length >= 2 && quoted[^1] == '"' ? quoted.Substring(1, quoted.Length - 2) : quoted.Trim('"');
			}

			if (tokens.IsPunctuation(next, '<'))
			{
				System.Text.StringBuilder path = new System.Text.StringBuilder();
				for (Int32 i = next + 1; i <= end && !tokens.IsPunctuation(i, '>'); i++)
				{
					path.Append(tokens.GetText(i));
				}

				return path.Length == 0 ? null : path.ToString();
			}

			return null;
		}

		/// <summary>Gets a value indicating whether the diagnostics reached the limit of the options; the analysis stops then.</summary>
		private Boolean LimitReached => this._diagnostics.Count >= this._options.MaximumCount;

		private Boolean Skip(Int32 index)
		{
			return this._tokens[index].InDirective || !this._frames[this._frameOf[index]].IsLive;
		}

		/// <summary>Finds the next token that is analysed after an index.</summary>
		private Int32 NextCode(Int32 index)
		{
			Int32 next = index + 1;
			while (next < this._count && this.Skip(next))
			{
				next++;
			}

			return next;
		}

		private Boolean IsIdentifier(Int32 index)
		{
			return index >= 0 && index < this._count && this._tokens[index].Kind == SslTokenKind.Identifier;
		}

		private Boolean IsKeyword(Int32 index, String keyword)
		{
			return this._tokens.IsKeyword(index, keyword);
		}

		private Boolean IsPunct(Int32 index, Char punctuation)
		{
			return this._tokens.IsPunctuation(index, punctuation);
		}

		private Boolean IsPunct(Int32 index, String punctuation)
		{
			return this._tokens.IsPunctuation(index, punctuation);
		}

		private ReadOnlySpan<Char> Span(Int32 index)
		{
			return this._tokens.Span(index);
		}

		/// <summary>Finds what is known about the macro of a name; macros are case sensitive.</summary>
		private MacroTraits? LookupMacro(ReadOnlySpan<Char> name)
		{
			if (this._localMacros.Count > 0 && this._localMacroLookup.TryGetValue(name, out MacroTraits? local))
			{
				return local;
			}

			if (this._environment.FindMacro is null)
			{
				return null;
			}

			if (this._macroCacheLookup.TryGetValue(name, out MacroTraits? cached))
			{
				return cached;
			}

			String text = name.ToString();
			ScriptMacro? macro = this._environment.FindMacro(text);
			MacroTraits? traits = macro is null ? null : MacroTraits.From(macro.Code);
			this._macroCache[text] = traits;
			return traits;
		}

		private void TaintGlobal(Int32 token)
		{
			if (this._globalTaintFrom < 0)
			{
				this._globalTaintFrom = token;
			}
		}

		/// <summary>Determines whether the names after a token can be unknown to the analysis: a header was not read, or a macro may have declared more.</summary>
		private Boolean IsUnreliable(Int32 token, ProcedureDeclaration? scope)
		{
			if (!this._environment.IsComplete)
			{
				return true;
			}

			if (this._globalTaintFrom >= 0 && token > this._globalTaintFrom)
			{
				return true;
			}

			return scope is not null && scope.TaintFrom >= 0 && token > scope.TaintFrom;
		}

		private void Report(ScriptDiagnosticRule rule, Int32 token, String[] arguments, ScriptQuickFix? fix = null, Boolean softenInConditional = false, ScriptDiagnosticSeverity? severityOverride = null)
		{
			if (this._diagnostics.Count >= this._options.MaximumCount)
			{
				return;
			}

			ScriptDiagnosticSeverity severity = severityOverride ?? rule.Severity;
			if (softenInConditional && severity == ScriptDiagnosticSeverity.Error && this._frameOf[token] != 0)
			{
				severity = ScriptDiagnosticSeverity.Warning;
			}

			SslToken at = this._tokens[token];
			this._diagnostics.Add(new ScriptDiagnostic(rule.Code, severity, rule.MessageKey, arguments, at.Offset, at.Length, at.Line, this._tokens.GetColumn(at.Offset)) { Fix = fix });
		}
	}
}