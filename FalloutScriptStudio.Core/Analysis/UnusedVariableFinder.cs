namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Finds the variables of a script that are declared but never read or never used (text analysis, see <see cref="SslScriptModel"/>).
	/// </summary>
	public static class UnusedVariableFinder
	{
		/// <summary>Finds the unused variables of a script text.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="options">What to report; the defaults when <see langword="null"/>.</param>
		/// <returns>The variables in text order.</returns>
		public static IReadOnlyList<UnusedVariable> Find(String text, UnusedVariableOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(text);
			return Find(SslScriptModel.Analyze(text), options);
		}

		/// <summary>Finds the unused variables of an analyzed script.</summary>
		/// <param name="model">The model of the script.</param>
		/// <param name="options">What to report; the defaults when <see langword="null"/>.</param>
		/// <returns>The variables in text order.</returns>
		public static IReadOnlyList<UnusedVariable> Find(SslScriptModel model, UnusedVariableOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(model);

			UnusedVariableOptions settings = options ?? new UnusedVariableOptions();
			Dictionary<VariableDeclaration, Int32> reads = [];
			Dictionary<VariableDeclaration, Int32> writes = [];
			foreach (VariableOccurrence occurrence in VariableOccurrenceFinder.Find(model))
			{
				Dictionary<VariableDeclaration, Int32> target = occurrence.IsWrite ? writes : reads;
				target[occurrence.Declaration] = target.GetValueOrDefault(occurrence.Declaration) + 1;
			}

			List<UnusedVariable> result = [];
			foreach (VariableDeclaration declaration in model.Declarations)
			{
				if (!IsReported(declaration, settings))
				{
					continue;
				}

				UnusedVariableStatus status = reads.GetValueOrDefault(declaration) > 0
					? UnusedVariableStatus.Used
					: writes.GetValueOrDefault(declaration) > 0 ? UnusedVariableStatus.WriteOnly : UnusedVariableStatus.Unused;
				if (status == UnusedVariableStatus.Used || (status == UnusedVariableStatus.WriteOnly && !settings.IncludeWriteOnly))
				{
					continue;
				}

				if (settings.IsMacroWord is { } isMacroWord && isMacroWord(declaration.Name))
				{
					continue;
				}

				Boolean removable = status == UnusedVariableStatus.Unused
					&& declaration.Kind is VariableDeclarationKind.Local or VariableDeclarationKind.Global
					&& !declaration.IsInParentheses
					&& !declaration.InitializerHasSideEffects;
				result.Add(new UnusedVariable(
					declaration.Name,
					declaration.Kind,
					status,
					declaration.ProcedureName,
					declaration.Offset,
					declaration.Line,
					declaration.Column,
					declaration.Name.Length,
					removable));
			}

			return result;
		}

		private static Boolean IsReported(VariableDeclaration declaration, UnusedVariableOptions options)
		{
			return declaration.Kind switch
			{
				VariableDeclarationKind.Parameter => options.IncludeParameters,
				VariableDeclarationKind.Local => true,
				VariableDeclarationKind.Global => options.IncludeGlobals,
				_ => false,
			};
		}

		/// <summary>Finds the unused variable whose declaration contains an offset.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="offset">The offset of the caret.</param>
		/// <param name="options">What to report; the defaults when <see langword="null"/>.</param>
		/// <returns>The variable, or <see langword="null"/> when the offset is not in the name of an unused variable.</returns>
		public static UnusedVariable? FindAt(String text, Int32 offset, UnusedVariableOptions? options = null)
		{
			return Find(text, options).FirstOrDefault(v => offset >= v.Offset && offset <= v.Offset + v.Length);
		}
	}
}