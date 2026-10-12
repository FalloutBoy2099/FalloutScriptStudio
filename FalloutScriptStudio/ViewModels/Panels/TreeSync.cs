namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

	/// <summary>
	/// Brings the nodes of a tree up to date with a freshly built copy of it, node by node. A tree that is thrown away and built again
	/// makes WPF create a container for every visible node again (about a millisecond each: 300 procedures freeze the window for a third
	/// of a second after every parse), and loses the expansion and the selection; nodes that are updated in place keep both.
	/// </summary>
	internal static class TreeSync
	{
		/// <summary>Updates the groups of a tree.</summary>
		/// <param name="groups">The groups shown.</param>
		/// <param name="fresh">The groups built from the new data.</param>
		/// <param name="inserted">Called for every node of <paramref name="fresh"/> that is new in the tree (to give it its expansion state).</param>
		/// <returns><see langword="false"/> when the groups are not the same ones (nothing was changed; the tree has to be rebuilt).</returns>
		public static Boolean TryMergeGroups(ObservableCollection<TreeGroupNodeViewModel> groups, IReadOnlyList<TreeGroupNodeViewModel> fresh, Action<TreeNodeViewModel> inserted)
		{
			if (groups.Count != fresh.Count)
			{
				return false;
			}

			for (Int32 i = 0; i < groups.Count; i++)
			{
				if (!String.Equals(groups[i].Key, fresh[i].Key, StringComparison.Ordinal) || groups[i].IsLocal != fresh[i].IsLocal)
				{
					return false;
				}
			}

			for (Int32 i = 0; i < groups.Count; i++)
			{
				MergeNode(groups[i], fresh[i], inserted);
			}

			return true;
		}

		private static void MergeNode(TreeNodeViewModel existing, TreeNodeViewModel fresh, Action<TreeNodeViewModel> inserted)
		{
			existing.Title = fresh.Title;
			existing.ToolTip = fresh.ToolTip;
			if (existing is ProcedureNodeViewModel procedure && fresh is ProcedureNodeViewModel freshProcedure)
			{
				procedure.Procedure = freshProcedure.Procedure;
			}
			else if (existing is VariableNodeViewModel variable && fresh is VariableNodeViewModel freshVariable)
			{
				variable.Variable = freshVariable.Variable;
			}

			MergeChildren(existing.Children, fresh.Children, inserted);
			if (existing is TreeGroupNodeViewModel group)
			{
				group.NotifyChildrenChanged();
			}
		}

		private static void MergeChildren(ObservableCollection<TreeNodeViewModel> existing, ObservableCollection<TreeNodeViewModel> fresh, Action<TreeNodeViewModel> inserted)
		{
			if (!HasUniqueKeys(existing) || !HasUniqueKeys(fresh))
			{
				existing.Clear();
				foreach (TreeNodeViewModel node in fresh)
				{
					existing.Add(node);
					inserted(node);
				}

				return;
			}

			for (Int32 i = 0; i < fresh.Count; i++)
			{
				TreeNodeViewModel wanted = fresh[i];
				if (i < existing.Count && SameNode(existing[i], wanted))
				{
					MergeNode(existing[i], wanted, inserted);
					continue;
				}

				Int32 found = -1;
				for (Int32 j = i + 1; j < existing.Count; j++)
				{
					if (SameNode(existing[j], wanted))
					{
						found = j;
						break;
					}
				}

				if (found >= 0)
				{
					existing.Move(found, i);
					MergeNode(existing[i], wanted, inserted);
				}
				else
				{
					existing.Insert(i, wanted);
					inserted(wanted);
				}
			}

			while (existing.Count > fresh.Count)
			{
				existing.RemoveAt(existing.Count - 1);
			}
		}

		private static Boolean SameNode(TreeNodeViewModel a, TreeNodeViewModel b)
		{
			return a.GetType() == b.GetType() && String.Equals(a.Key, b.Key, StringComparison.Ordinal);
		}

		/// <summary>Determines whether no two nodes of a list have the same key (only then can the nodes be paired by key).</summary>
		private static Boolean HasUniqueKeys(ObservableCollection<TreeNodeViewModel> nodes)
		{
			HashSet<String> keys = new HashSet<String>(nodes.Count, StringComparer.Ordinal);
			foreach (TreeNodeViewModel node in nodes)
			{
				if (!keys.Add(node.Key))
				{
					return false;
				}
			}

			return true;
		}
	}
}
