namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using Parsing;

	/// <summary>Works out what the name written as the target of an option is: a node, another procedure, a macro that stands for one of them, or nothing known.</summary>
	internal sealed class TargetResolver
	{
		private const Int32 MaxMacroDepth = 8;

		private readonly Dictionary<String, DialogueGraphNode> _nodes;
		private readonly HashSet<String> _procedures;
		private readonly HashSet<String> _variables;

		/// <summary>Initializes a new instance of the <see cref="TargetResolver"/> class.</summary>
		/// <param name="info">The parse result (the macros and the variables).</param>
		/// <param name="nodes">The nodes of the script by name (the case is ignored).</param>
		/// <param name="procedures">The names of every procedure the script defines or declares (the case is ignored).</param>
		/// <param name="variables">The names of the variables the text declares.</param>
		public TargetResolver(ProgramInfo info, Dictionary<String, DialogueGraphNode> nodes, HashSet<String> procedures, IEnumerable<String> variables)
		{
			this.Info = info;
			this._nodes = nodes;
			this._procedures = procedures;
			this._variables = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			foreach (ScriptVariable variable in info.Variables)
			{
				this._variables.Add(variable.Name);
			}

			foreach (String variable in variables)
			{
				this._variables.Add(variable);
			}
		}

		/// <summary>Gets the parse result.</summary>
		public ProgramInfo Info { get; }

		/// <summary>Resolves a target.</summary>
		/// <param name="raw">The name as the parser read it.</param>
		/// <returns>The kind and the name of the node or procedure (the name as written for the other kinds).</returns>
		public (TargetKind Kind, String Name) Resolve(String raw)
		{
			String name = raw.Trim().TrimStart('@').Trim();
			for (Int32 depth = 0; depth <= MaxMacroDepth; depth++)
			{
				if (!IsIdentifier(name))
				{
					return (TargetKind.Unknown, name);
				}

				if (this._nodes.TryGetValue(name, out DialogueGraphNode? node))
				{
					return (TargetKind.Node, node.Name);
				}

				if (this._procedures.Contains(name))
				{
					return (TargetKind.Procedure, name);
				}

				ScriptMacro? macro = this.Info.FindMacro(name);
				if (macro is not null)
				{
					if (!String.Equals(macro.DefinitionName, macro.Token, StringComparison.Ordinal))
					{
						return (TargetKind.Unknown, name);
					}

					name = StripParentheses(macro.Code.Trim());
					continue;
				}

				return this._variables.Contains(name) ? (TargetKind.Unknown, name) : (TargetKind.Undefined, name);
			}

			return (TargetKind.Unknown, name);
		}

		private static String StripParentheses(String code)
		{
			while (code.Length > 1 && code[0] == '(' && code[^1] == ')')
			{
				code = code.Substring(1, code.Length - 2).Trim();
			}

			return code;
		}

		private static Boolean IsIdentifier(String name)
		{
			if (name.Length == 0 || !(Char.IsLetter(name[0]) || name[0] == '_'))
			{
				return false;
			}

			foreach (Char c in name)
			{
				if (!(Char.IsLetterOrDigit(c) || c == '_'))
				{
					return false;
				}
			}

			return true;
		}
	}
}