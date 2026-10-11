namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Localization;

	/// <summary>Builds the procedures and variables trees from the parse data of a script (legacy <c>UpdateNames</c>).</summary>
	public static class SymbolTreeBuilder
	{
		/// <summary>Key of the group of procedures declared in headers.</summary>
		public const String GlobalProceduresKey = "group:global-procedures";

		/// <summary>Key of the group of procedures of the current script.</summary>
		public const String LocalProceduresKey = "group:local-procedures";

		/// <summary>Key of the group of variables declared in headers.</summary>
		public const String GlobalVariablesKey = "group:global-variables";

		/// <summary>Key of the group of variables of the current script.</summary>
		public const String LocalVariablesKey = "group:local-variables";

		/// <summary>
		/// Builds the "Global Procedures" and "Local Procedures" groups. Without native parser data only the local group is returned.
		/// </summary>
		/// <param name="info">The parse data.</param>
		/// <param name="documentFileName">The file name of the script the data belongs to.</param>
		/// <param name="showArguments">True to show the argument list next to every name.</param>
		public static IReadOnlyList<TreeGroupNodeViewModel> BuildProcedureGroups(ProgramInfo info, String documentFileName, Boolean showArguments)
		{
			ArgumentNullException.ThrowIfNull(info);

			TreeGroupNodeViewModel global = new TreeGroupNodeViewModel(Loc.Get("Str.Panels.GroupGlobalProcedures"), GlobalProceduresKey, false)
			{
				ToolTip = WithHint("Str.Panels.TipGlobalProcedures", "Str.Panels.HintProcedure"),
			};
			TreeGroupNodeViewModel local = new TreeGroupNodeViewModel(Loc.Get("Str.Panels.GroupLocalProcedures"), LocalProceduresKey, true)
			{
				ToolTip = WithHint("Str.Panels.TipLocalProcedures", "Str.Panels.HintProcedure"),
			};

			List<ProcedureNodeViewModel> locals = new List<ProcedureNodeViewModel>();
			foreach (ScriptProcedure procedure in info.Procedures)
			{
				if (!info.HasNativeData && procedure.EndLine == -1)
				{
					continue;
				}

				Boolean isLocal = IsLocal(procedure, documentFileName);
				ProcedureNodeViewModel node = new ProcedureNodeViewModel(
					procedure,
					showArguments ? procedure.ShortDescription : procedure.Name,
					"proc:" + procedure.NormalizedName,
					isLocal)
				{
					ToolTip = isLocal
						? procedure.FullDescription
						: procedure.FullDescription + "\n" + Loc.Format("Str.Panels.DeclaredIn", procedure.FileName),
				};

				foreach (ScriptVariable variable in procedure.Variables)
				{
					node.Children.Add(new VariableNodeViewModel(variable, "var:" + procedure.NormalizedName + "/" + variable.Name.ToLowerInvariant())
					{
						ToolTip = variable.FullDescription,
					});
				}

				if (isLocal)
				{
					locals.Add(node);
				}
				else
				{
					global.Children.Add(node);
				}
			}

			foreach (ProcedureNodeViewModel node in locals.OrderBy(n => n.Procedure.StartLine > 0 ? n.Procedure.StartLine : Int32.MaxValue))
			{
				local.Children.Add(node);
			}

			return info.HasNativeData ? new[] { global, local } : new[] { local };
		}

		/// <summary>Builds the "Global Variables" and "Script Variables" groups. Empty without native parser data.</summary>
		public static IReadOnlyList<TreeGroupNodeViewModel> BuildVariableGroups(ProgramInfo info, String documentFileName)
		{
			ArgumentNullException.ThrowIfNull(info);
			if (!info.HasNativeData)
			{
				return Array.Empty<TreeGroupNodeViewModel>();
			}

			TreeGroupNodeViewModel global = new TreeGroupNodeViewModel(Loc.Get("Str.Panels.GroupGlobalVariables"), GlobalVariablesKey, false)
			{
				ToolTip = WithHint("Str.Panels.TipGlobalVariables", "Str.Panels.HintVariable"),
			};
			TreeGroupNodeViewModel local = new TreeGroupNodeViewModel(Loc.Get("Str.Panels.GroupScriptVariables"), LocalVariablesKey, true)
			{
				ToolTip = WithHint("Str.Panels.TipScriptVariables", "Str.Panels.HintVariable"),
			};

			foreach (ScriptVariable variable in info.Variables)
			{
				Boolean isLocal = String.IsNullOrEmpty(variable.FileName)
					|| String.Equals(variable.FileName, documentFileName, StringComparison.OrdinalIgnoreCase);
				VariableNodeViewModel node = new VariableNodeViewModel(variable, "var:" + variable.Name.ToLowerInvariant())
				{
					ToolTip = isLocal ? variable.FullDescription : variable.FullDescription + "\n" + Loc.Format("Str.Panels.DeclaredIn", variable.FileName),
				};
				(isLocal ? local : global).Children.Add(node);
			}

			return new[] { global, local };
		}

		private static String WithHint(String textKey, String hintKey)
		{
			return Loc.Get(textKey) + "\n\n " + Loc.Get(hintKey);
		}

		private static Boolean IsLocal(ScriptProcedure procedure, String documentFileName)
		{
			if (procedure.IsImported)
			{
				return false;
			}

			return String.IsNullOrEmpty(procedure.FileName)
				|| String.Equals(procedure.FileName, documentFileName, StringComparison.OrdinalIgnoreCase);
		}
	}
}
