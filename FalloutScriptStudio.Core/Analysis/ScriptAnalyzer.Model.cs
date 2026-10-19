namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>The data the analysis of one script builds: conditional blocks, procedures and variables.</summary>
	internal sealed partial class ScriptAnalyzer
	{
		private enum ProcedureKind
		{
			/// <summary><c>procedure name;</c></summary>
			Forward,

			/// <summary><c>procedure name begin ... end</c></summary>
			Definition,

			/// <summary><c>import procedure name;</c></summary>
			Import,

			/// <summary><c>export procedure name;</c></summary>
			ExportForward,
		}

		private enum VariableScopeKind
		{
			Global,
			Local,
			Parameter,
			Loop,
		}

		/// <summary>One branch of an <c>#if</c> / <c>#ifdef</c> / <c>#ifndef</c> group.</summary>
		private sealed class ConditionFrame
		{
			public ConditionFrame(Int32 index, ConditionFrame? parent, Int32 group, Int32 branch, Int32 liveBranch)
			{
				this.Index = index;
				this.Parent = parent;
				this.Group = group;
				this.Branch = branch;
				this.LiveBranch = liveBranch;
				this.IsLive = (parent is null || parent.IsLive) && branch == liveBranch;
			}

			public Int32 Index { get; }

			public ConditionFrame? Parent { get; }

			public Int32 Group { get; }

			public Int32 Branch { get; }

			/// <summary>Gets the branch of the group that the analysis follows (the first one, or the second one after <c>#if 0</c>).</summary>
			public Int32 LiveBranch { get; }

			/// <summary>Gets a value indicating whether the code of the branch is analysed.</summary>
			public Boolean IsLive { get; }
		}

		private sealed class ParameterDeclaration
		{
			public ParameterDeclaration(String name, Int32 token, Boolean hasDefault)
			{
				this.Name = name;
				this.Token = token;
				this.HasDefault = hasDefault;
			}

			public String Name { get; }

			public Int32 Token { get; }

			public Boolean HasDefault { get; }
		}

		private sealed class VariableDeclarationInfo
		{
			public VariableDeclarationInfo(String name, Int32 token, VariableScopeKind scope, ProcedureDeclaration? owner, Int32 frame)
			{
				this.Name = name;
				this.Token = token;
				this.Scope = scope;
				this.Owner = owner;
				this.Frame = frame;
			}

			public String Name { get; }

			public Int32 Token { get; }

			public VariableScopeKind Scope { get; }

			public ProcedureDeclaration? Owner { get; }

			public Int32 Frame { get; }

			public Boolean HasInitializer { get; set; }

			/// <summary>Gets or sets the index of the <c>variable</c> keyword (or of the first modifier) of the statement.</summary>
			public Int32 StatementToken { get; set; }

			public Boolean IsInParentheses { get; set; }
		}

		private sealed class ProcedureDeclaration
		{
			public ProcedureDeclaration(String name, Int32 nameToken, Int32 firstToken, ProcedureKind kind, Int32 frame)
			{
				this.Name = name;
				this.NameToken = nameToken;
				this.FirstToken = firstToken;
				this.Kind = kind;
				this.Frame = frame;
			}

			public String Name { get; }

			public Int32 NameToken { get; }

			public Int32 FirstToken { get; }

			public ProcedureKind Kind { get; set; }

			public Int32 Frame { get; }

			public Boolean IsImport { get; set; }

			public Boolean IsExport { get; set; }

			public Boolean HasModifiers { get; set; }

			public List<ParameterDeclaration> Parameters { get; } = [];

			public Int32 MinArguments { get; set; }

			public Int32 MaxArguments { get; set; }

			public Boolean HasDefaults { get; set; }

			public Int32 BeginToken { get; set; } = -1;

			public Int32 EndToken { get; set; } = -1;

			public Int32 SemicolonToken { get; set; } = -1;

			public Boolean IsClosed { get; set; }

			/// <summary>Gets or sets the token after which a macro that may declare variables was used in the body, or -1.</summary>
			public Int32 TaintFrom { get; set; } = -1;

			public Dictionary<String, List<VariableDeclarationInfo>> Locals { get; } = new(StringComparer.OrdinalIgnoreCase);

			public List<VariableDeclarationInfo> LocalList { get; } = [];
		}

		private readonly record struct IncludeDirective(Int32 Token, String Path);

		private readonly record struct PendingDiagnostic(ScriptDiagnosticRule Rule, Int32 Token, String[] Arguments);
	}
}