namespace FalloutScriptStudio.Core.Functions
{
	using System;

	/// <summary>
	/// The user-editable parts of a function: what the "add function" / "edit function" dialog collects.
	/// </summary>
	/// <param name="Name">The name shown in the tree.</param>
	/// <param name="Description">The short description (tool tip).</param>
	/// <param name="Code">The code inserted into the script; real line breaks are stored as <c>&lt;cr&gt;</c>.</param>
	public sealed record FunctionDefinition(String Name, String Description, String Code)
	{
		/// <summary>Creates a definition from the code of an existing function node.</summary>
		/// <param name="node">A function node.</param>
		/// <returns>The definition, with <c>&lt;cr&gt;</c> markers shown as real line breaks.</returns>
		public static FunctionDefinition FromNode(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return new FunctionDefinition(node.Name, node.Description, node.Code.Replace(FunctionCatalogParser.LineBreakMarker, Environment.NewLine, StringComparison.Ordinal));
		}

		/// <summary>
		/// Checks the definition.
		/// </summary>
		/// <param name="error">Receives the reason when the definition is not acceptable.</param>
		/// <returns><c>false</c> when the name is empty or when the name or description would break the file format.</returns>
		public Boolean TryValidate(out String? error)
		{
			if (String.IsNullOrWhiteSpace(this.Name))
			{
				error = "The function name is empty.";
				return false;
			}

			String[] reserved =
			{
				FunctionCatalogParser.DescriptionMarker,
				FunctionCatalogParser.CodeMarker,
				FunctionCatalogParser.LineBreakMarker,
			};
			foreach (String marker in reserved)
			{
				if (this.Name.Contains(marker, StringComparison.Ordinal) || this.Description.Contains(marker, StringComparison.Ordinal))
				{
					error = "The name and the description cannot contain " + marker + ".";
					return false;
				}
			}

			error = null;
			return true;
		}

		/// <summary>Formats the definition as a line of the ini file (line breaks of the code become <c>&lt;cr&gt;</c>).</summary>
		/// <returns>The line.</returns>
		public String ToIniLine()
		{
			String name = SingleLine(this.Name);
			String description = SingleLine(this.Description);
			String code = this.Code.Trim().Replace("\r\n", FunctionCatalogParser.LineBreakMarker, StringComparison.Ordinal)
				.Replace("\n", FunctionCatalogParser.LineBreakMarker, StringComparison.Ordinal)
				.Replace("\r", FunctionCatalogParser.LineBreakMarker, StringComparison.Ordinal);
			return name + FunctionCatalogParser.DescriptionMarker + description + FunctionCatalogParser.CodeMarker + code;
		}

		private static String SingleLine(String text)
		{
			return text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Trim();
		}
	}
}
