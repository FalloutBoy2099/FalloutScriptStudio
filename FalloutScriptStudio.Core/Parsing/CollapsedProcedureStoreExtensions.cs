namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Helpers that connect <see cref="ICollapsedProcedureStore"/> with <see cref="FoldingRegion"/> lists.
	/// </summary>
	public static class CollapsedProcedureStoreExtensions
	{
		/// <summary>
		/// Remembers the collapsed state of the procedure regions (legacy <c>SetProceduresCollapsed</c>); call it before the document is closed.
		/// </summary>
		/// <param name="store">The store.</param>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="regions">The regions currently shown by the editor.</param>
		/// <param name="isFolded">Tells whether the editor shows a region collapsed.</param>
		public static void Remember(this ICollapsedProcedureStore store, String scriptName, IEnumerable<FoldingRegion> regions, Func<FoldingRegion, Boolean> isFolded)
		{
			foreach (FoldingRegion region in regions.Where(r => r.Kind == FoldingKind.Procedure))
			{
				store.SetCollapsed(scriptName, region.Title, isFolded(region));
			}
		}

		/// <summary>
		/// Selects the procedure regions that were collapsed last time (legacy <c>GetProceduresCollapse</c>).
		/// </summary>
		/// <param name="store">The store.</param>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="regions">The regions to examine.</param>
		/// <returns>The procedure regions that should be collapsed.</returns>
		public static IReadOnlyList<FoldingRegion> GetCollapsedRegions(this ICollapsedProcedureStore store, String scriptName, IEnumerable<FoldingRegion> regions)
		{
			return regions
				.Where(r => r.Kind == FoldingKind.Procedure && store.IsCollapsed(scriptName, r.Title))
				.ToList();
		}
	}
}