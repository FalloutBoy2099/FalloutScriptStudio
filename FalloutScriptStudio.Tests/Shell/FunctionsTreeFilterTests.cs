namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Linq;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	/// <summary>
	/// The functions tree shows only the nodes that pass the filter: a node that is filtered out has no item in the tree at all, which is
	/// what keeps a filter in a catalog of a thousand functions fast (the tree creates an item for every node of an expanded category).
	/// </summary>
	public sealed class FunctionsTreeFilterTests
	{
		private static readonly String[] _catalog =
		{
			"<m>Objects",
			"obj_name<d>The name<s>obj_name(obj)",
			"obj_pid<d>The pid<s>obj_pid(obj)",
			"move_to<d>Moves<s>move_to(obj, tile, elev)",
			"<m+>Inventory",
			"obj_carrying<d>Carry<s>obj_carrying(obj)",
			"add_item<d>Add<s>add_item(obj, item)",
			"<m->",
			"<m>Math",
			"random<d>Random<s>random(1, 2)",
			"abs<d>Absolute<s>abs(x)",
		};

		private static List<FunctionNodeViewModel> CreateRoots()
		{
			return FunctionCatalogParser.Parse(_catalog, FunctionSource.BuiltIn).Roots.Select(node => new FunctionNodeViewModel(node, null)).ToList();
		}

		private static void AssertShowsTheVisibleNodesInOrder(IEnumerable<FunctionNodeViewModel> nodes)
		{
			foreach (FunctionNodeViewModel node in nodes)
			{
				Assert.Equal(node.Children.Where(child => child.IsFilterVisible), node.VisibleChildren);
				AssertShowsTheVisibleNodesInOrder(node.Children);
			}
		}

		[Fact]
		public void TheListOfVisibleChildrenStartsWithAllTheChildren()
		{
			List<FunctionNodeViewModel> roots = CreateRoots();

			Assert.All(roots, root => Assert.Equal(root.Children, root.VisibleChildren));
			Assert.Equal(new[] { "obj_name", "obj_pid", "move_to", "Inventory" }, roots[0].VisibleChildren.Select(child => child.Name));
		}

		[Fact]
		public void FilteringRemovesTheNodesThatDoNotMatch_AndClearingTheFilterBringsThemBackInOrder()
		{
			List<FunctionNodeViewModel> roots = CreateRoots();

			TreeFilter.Apply(roots, "obj_p");

			AssertShowsTheVisibleNodesInOrder(roots);
			Assert.Equal(new[] { "obj_pid" }, roots[0].VisibleChildren.Select(child => child.Name));
			Assert.True(roots[0].IsFilterVisible);
			Assert.False(roots[1].IsFilterVisible);

			TreeFilter.Apply(roots, "obj_");
			AssertShowsTheVisibleNodesInOrder(roots);
			Assert.Equal(new[] { "obj_name", "obj_pid", "Inventory" }, roots[0].VisibleChildren.Select(child => child.Name));

			TreeFilter.Apply(roots, String.Empty);
			AssertShowsTheVisibleNodesInOrder(roots);
			Assert.Equal(roots[0].Children, roots[0].VisibleChildren);
		}

		[Fact]
		public void ACategoryThatMatchesShowsAllItsFunctions()
		{
			List<FunctionNodeViewModel> roots = CreateRoots();

			TreeFilter.Apply(roots, "inventory");

			Assert.Equal(new[] { "Inventory" }, roots[0].VisibleChildren.Select(child => child.Name));
			Assert.Equal(new[] { "obj_carrying", "add_item" }, roots[0].Children.Single(child => child.Name == "Inventory").VisibleChildren.Select(child => child.Name));
		}

		[Fact]
		public void TheNodesThatStayAreNotRecreated_OnlyTheChangedOnesAreInsertedOrRemoved()
		{
			List<FunctionNodeViewModel> roots = CreateRoots();
			List<NotifyCollectionChangedEventArgs> changes = [];
			roots[0].VisibleChildren.CollectionChanged += (_, e) => changes.Add(e);
			TreeFilter.Apply(roots, "obj_");
			changes.Clear();

			TreeFilter.Apply(roots, "obj_p");

			Assert.Equal(2, changes.Count(e => e.Action == NotifyCollectionChangedAction.Remove));
			Assert.DoesNotContain(changes, e => e.Action == NotifyCollectionChangedAction.Add);
			Assert.DoesNotContain(changes, e => e.Action == NotifyCollectionChangedAction.Reset);
		}

		[Fact]
		public void AnyChainOfFilters_KeepsTheListsInStepWithTheVisibilityOfTheNodes()
		{
			List<FunctionNodeViewModel> roots = CreateRoots();
			String[] texts = { "o", "ob", "obj", "obj_", "abs", "ran", "x", "mo", "move", "n", "e", String.Empty, "ITEM", "inv", "r", "a", "zz", "", "_", "p" };

			foreach (String text in texts.Concat(texts.Reverse()))
			{
				TreeFilter.Apply(roots, text);
				AssertShowsTheVisibleNodesInOrder(roots);
			}
		}

		[Fact]
		public void ThePanelListsTheVisibleRoots_AndFollowsTheCatalog()
		{
			using ShellHarness harness = new ShellHarness();
			Assert.Equal(new[] { "Group", "User" }, harness.Functions.VisibleRoots.Select(root => root.Name));

			harness.Functions.FilterText = "wai";
			Assert.Equal(new[] { "Group" }, harness.Functions.VisibleRoots.Select(root => root.Name));
			Assert.Equal(new[] { "wait" }, harness.Functions.VisibleRoots[0].VisibleChildren.Select(child => child.Name));

			harness.Functions.FilterText = "zzzz";
			Assert.Empty(harness.Functions.VisibleRoots);

			harness.Functions.FilterText = String.Empty;
			Assert.Equal(new[] { "Group", "User" }, harness.Functions.VisibleRoots.Select(root => root.Name));

			harness.Functions.FilterText = "mine";
			harness.Functions.Reload();
			Assert.Equal(new[] { "User" }, harness.Functions.VisibleRoots.Select(root => root.Name));
			Assert.Same(harness.Functions.Roots[1], harness.Functions.VisibleRoots[0]);
		}
	}
}