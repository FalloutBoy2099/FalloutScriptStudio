namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Linq;
	using Core.Analysis;
	using Localization;

	/// <summary>The words that describe a quick fix of the semantic analysis to the user (tool tip, problem list, menu), in the language in use.</summary>
	public static class ScriptFixText
	{
		/// <summary>Describes a fix.</summary>
		/// <param name="fix">The fix.</param>
		/// <returns>The text, for example <c>Did you mean 'display_msg'?</c>.</returns>
		public static String Describe(ScriptQuickFix fix)
		{
			ArgumentNullException.ThrowIfNull(fix);
			return Describe(fix.Id, fix.Argument);
		}

		/// <summary>The gesture of the Quick Fix command, as the tool tips and the documentation write it.</summary>
		public const String QuickFixGestureText = "Ctrl+.";

		/// <summary>Writes the message of a finding as the tool tip of its underline shows it: the message, then the quick fix with its gesture when there is one.</summary>
		/// <param name="diagnostic">The finding.</param>
		/// <returns>The text, in the language in use.</returns>
		public static String DescribeDiagnostic(ScriptDiagnostic diagnostic)
		{
			ArgumentNullException.ThrowIfNull(diagnostic);
			String message = Loc.Format(diagnostic.MessageKey, diagnostic.MessageArguments.Cast<Object?>().ToArray());
			return diagnostic.Fix is { } fix
				? message + Environment.NewLine + Loc.Format("Str.Editor.Diag.QuickFixHint", QuickFixGestureText, Describe(fix))
				: message;
		}

		/// <summary>Describes a fix by its kind.</summary>
		/// <param name="id">The kind of the fix, see <see cref="ScriptQuickFix"/>.</param>
		/// <param name="argument">The argument of the fix (the suggested name, the forward declaration).</param>
		/// <returns>The text.</returns>
		public static String Describe(String id, String argument)
		{
			return id switch
			{
				ScriptQuickFix.RenameId => Loc.Format("Str.Editor.Fix.Rename", argument),
				ScriptQuickFix.ForwardDeclareId => Loc.Format("Str.Editor.Fix.ForwardDeclare", argument),
				ScriptQuickFix.RemoveDeclarationId => Loc.Get("Str.Editor.Fix.RemoveDeclaration"),
				_ => Loc.Get("Str.Editor.Fix.Generic"),
			};
		}
	}
}