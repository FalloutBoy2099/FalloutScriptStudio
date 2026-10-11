namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The tree of the functions menu: the categories of <c>functions.ini</c> followed by those of <c>userfunctions.ini</c>.
	/// </summary>
	public sealed class FunctionCatalog
	{
		/// <summary>The deepest category level: a top level category (0) holds sub categories (1) that hold sub-sub categories (2).</summary>
		public const Int32 MaxCategoryDepth = 2;

		/// <summary>
		/// Initializes a new instance of the <see cref="FunctionCatalog"/> class.
		/// </summary>
		/// <param name="roots">The top level nodes.</param>
		/// <param name="issues">The problems found while reading the files.</param>
		public FunctionCatalog(IReadOnlyList<FunctionNode> roots, IReadOnlyList<FunctionCatalogIssue> issues)
		{
			ArgumentNullException.ThrowIfNull(roots);
			ArgumentNullException.ThrowIfNull(issues);
			this.Roots = roots;
			this.Issues = issues;
		}

		/// <summary>Gets an empty catalog.</summary>
		public static FunctionCatalog Empty { get; } = new FunctionCatalog(Array.Empty<FunctionNode>(), Array.Empty<FunctionCatalogIssue>());

		/// <summary>Gets the top level nodes (categories and separators) in menu order.</summary>
		public IReadOnlyList<FunctionNode> Roots { get; }

		/// <summary>Gets the problems found while reading the files.</summary>
		public IReadOnlyList<FunctionCatalogIssue> Issues { get; }

		/// <summary>Enumerates every node of the tree, parents before their children.</summary>
		/// <returns>All nodes.</returns>
		public IEnumerable<FunctionNode> Flatten()
		{
			return Flatten(this.Roots);
		}

		/// <summary>Enumerates every function of the tree.</summary>
		/// <returns>All functions.</returns>
		public IEnumerable<FunctionNode> GetFunctions()
		{
			return this.Flatten().Where(n => n.Kind == FunctionNodeKind.Function);
		}

		/// <summary>Finds functions by name.</summary>
		/// <param name="name">The function name.</param>
		/// <param name="comparison">How names are compared.</param>
		/// <returns>The functions with that name.</returns>
		public IEnumerable<FunctionNode> FindFunctions(String name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
		{
			ArgumentNullException.ThrowIfNull(name);
			return this.GetFunctions().Where(f => String.Equals(f.Name, name, comparison));
		}

		/// <summary>Finds the parent of a node.</summary>
		/// <param name="node">A node of this catalog.</param>
		/// <returns>The parent category, or <c>null</c> for a top level node.</returns>
		public FunctionNode? GetParent(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return this.Flatten().FirstOrDefault(n => n.Children.Contains(node));
		}

		/// <summary>
		/// Finds the node that corresponds to a node of an older catalog (same file, line and text).
		/// </summary>
		/// <param name="node">A node of a previous catalog.</param>
		/// <returns>The matching node, or <c>null</c>.</returns>
		public FunctionNode? FindEquivalent(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return this.Flatten().FirstOrDefault(n => n.Source == node.Source && n.SourceLine == node.SourceLine && n.RawLine == node.RawLine);
		}

		private static IEnumerable<FunctionNode> Flatten(IEnumerable<FunctionNode> nodes)
		{
			foreach (FunctionNode node in nodes)
			{
				yield return node;
				foreach (FunctionNode child in Flatten(node.Children))
				{
					yield return child;
				}
			}
		}
	}
}
