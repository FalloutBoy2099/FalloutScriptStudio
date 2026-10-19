namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using TextTools;

	/// <summary>Turns the <see cref="ScriptQuickFix"/> of a diagnostic into the text edits that carry it out.</summary>
	public static class ScriptQuickFixes
	{
		/// <summary>The argument of <see cref="ScriptQuickFix.RemoveDeclarationId"/> for a whole statement (<c>procedure name;</c>).</summary>
		public const String StatementTarget = "statement";

		/// <summary>The argument of <see cref="ScriptQuickFix.RemoveDeclarationId"/> for one variable of a <c>variable</c> statement.</summary>
		public const String VariableTarget = "variable";

		/// <summary>Creates the edits of a fix for the text it is applied to.</summary>
		/// <param name="text">The current text of the script (the fix was found in the same text).</param>
		/// <param name="fix">The fix.</param>
		/// <returns>The edits, in the coordinates of <paramref name="text"/>; empty when the text is not what the fix was made for.</returns>
		public static IReadOnlyList<TextEdit> CreateEdits(String text, ScriptQuickFix fix)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(fix);

			if (fix.TargetOffset < 0 || fix.TargetOffset + fix.TargetLength > text.Length)
			{
				return Array.Empty<TextEdit>();
			}

			switch (fix.Id)
			{
				case ScriptQuickFix.RenameId:
					return fix.TargetLength > 0 && fix.Argument.Length > 0
						? new[] { new TextEdit(fix.TargetOffset, fix.TargetLength, fix.Argument) }
						: Array.Empty<TextEdit>();
				case ScriptQuickFix.ForwardDeclareId:
					return fix.Argument.Length > 0
						? new[] { TextEdit.Insert(fix.TargetOffset, fix.Argument + GetNewLine(text)) }
						: Array.Empty<TextEdit>();
				case ScriptQuickFix.RemoveDeclarationId:
					return CreateRemoval(text, fix);
				default:
					return Array.Empty<TextEdit>();
			}
		}

		/// <summary>Finds the line break style of a text.</summary>
		/// <param name="text">The text.</param>
		/// <returns><c>\n</c> when the first line break is a bare line feed, otherwise <c>\r\n</c>.</returns>
		public static String GetNewLine(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			Int32 index = text.IndexOf('\n', StringComparison.Ordinal);
			return index < 0 || (index > 0 && text[index - 1] == '\r') ? "\r\n" : "\n";
		}

		private static TextEdit[] CreateRemoval(String text, ScriptQuickFix fix)
		{
			if (String.Equals(fix.Argument, StatementTarget, StringComparison.Ordinal))
			{
				DeclarationStatement statement = new DeclarationStatement(fix.TargetOffset, false) { End = fix.TargetOffset + fix.TargetLength };
				return new[] { VariableDeclarationEditor.CreateStatementRemoval(text, statement) };
			}

			if (String.Equals(fix.Argument, VariableTarget, StringComparison.Ordinal))
			{
				SslScriptModel model = SslScriptModel.Analyze(text);
				foreach (VariableDeclaration declaration in model.Declarations)
				{
					if (declaration.Offset == fix.TargetOffset)
					{
						return new[] { VariableDeclarationEditor.CreateRemoval(text, declaration) };
					}
				}
			}

			return Array.Empty<TextEdit>();
		}
	}
}