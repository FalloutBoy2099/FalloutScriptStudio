namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Linq;
	using TextTools;

	/// <summary>Computes the text edits that remove the declaration of a variable.</summary>
	public static class VariableDeclarationEditor
	{
		/// <summary>Creates the edit that removes an unused variable that <see cref="UnusedVariableFinder"/> reported.</summary>
		/// <param name="text">The script text the variable was found in.</param>
		/// <param name="variable">The variable.</param>
		/// <returns>The edit, or <see langword="null"/> when the variable cannot be removed safely or is not at that place any more.</returns>
		public static TextEdit? CreateRemoval(String text, UnusedVariable variable)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(variable);

			if (!variable.CanRemove)
			{
				return null;
			}

			SslScriptModel model = SslScriptModel.Analyze(text);
			VariableDeclaration? declaration = model.Declarations.FirstOrDefault(d => d.Offset == variable.Offset && String.Equals(d.Name, variable.Name, StringComparison.OrdinalIgnoreCase));
			return declaration is null ? null : CreateRemoval(text, declaration);
		}

		/// <summary>Creates the edit that removes the declaration of a variable (the variable alone, or the whole statement when it is the only one in it).</summary>
		/// <param name="text">The script text the declaration comes from.</param>
		/// <param name="declaration">The declaration.</param>
		/// <returns>The edit.</returns>
		public static TextEdit CreateRemoval(String text, VariableDeclaration declaration)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(declaration);

			DeclarationStatement statement = declaration.Statement;
			if (statement.Items.Count > 1)
			{
				if (declaration.ItemIndex > 0)
				{
					Int32 start = statement.Items[declaration.ItemIndex - 1].ItemEnd;
					return TextEdit.Delete(start, declaration.ItemEnd - start);
				}

				Int32 next = statement.Items[1].ItemStart;
				return TextEdit.Delete(declaration.ItemStart, next - declaration.ItemStart);
			}

			return CreateStatementRemoval(text, statement);
		}

		/// <summary>Creates the edit that removes a whole declaration statement; when it is alone on its lines the lines are removed.</summary>
		/// <param name="text">The script text the statement comes from.</param>
		/// <param name="statement">The statement.</param>
		/// <returns>The edit.</returns>
		public static TextEdit CreateStatementRemoval(String text, DeclarationStatement statement)
		{
			Int32 start = statement.Offset;
			Int32 end = Math.Min(statement.End, text.Length);

			Int32 lineStart = start;
			while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
			{
				lineStart--;
			}

			Int32 lineEnd = end;
			while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
			{
				lineEnd++;
			}

			Boolean blankBefore = IsBlank(text, lineStart, start);
			String rest = text.Substring(end, lineEnd - end);
			Boolean restIsBlank = rest.AsSpan().Trim().Length == 0;
			Boolean restIsComment = rest.AsSpan().TrimStart().StartsWith("//", StringComparison.Ordinal);
			if (blankBefore && (restIsBlank || restIsComment))
			{
				Int32 terminatorEnd = lineEnd;
				if (terminatorEnd < text.Length && text[terminatorEnd] == '\r')
				{
					terminatorEnd++;
				}

				if (terminatorEnd < text.Length && text[terminatorEnd] == '\n')
				{
					terminatorEnd++;
				}

				return TextEdit.Delete(lineStart, terminatorEnd - lineStart);
			}

			if (restIsBlank)
			{
				Int32 before = start;
				while (before > lineStart && (text[before - 1] == ' ' || text[before - 1] == '\t'))
				{
					before--;
				}

				return TextEdit.Delete(before, end - before);
			}

			Int32 after = end;
			while (after < lineEnd && (text[after] == ' ' || text[after] == '\t'))
			{
				after++;
			}

			return TextEdit.Delete(start, after - start);
		}

		private static Boolean IsBlank(String text, Int32 start, Int32 end)
		{
			for (Int32 i = start; i < end; i++)
			{
				if (text[i] != ' ' && text[i] != '\t')
				{
					return false;
				}
			}

			return true;
		}
	}
}