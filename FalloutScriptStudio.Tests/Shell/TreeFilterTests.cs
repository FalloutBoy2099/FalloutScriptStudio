namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	public class TreeFilterTests
	{
		[Fact]
		public void EmptyFilterShowsEverything()
		{
			TestNode root = Tree();

			Boolean any = TreeFilter.Apply(new[] { root }, "  ");

			Assert.True(any);
			Assert.All(Flatten(root), node => Assert.True(node.IsFilterVisible));
		}

		[Fact]
		public void MatchingLeafShowsItsAncestorsAndExpandsThem()
		{
			TestNode root = Tree();

			Boolean any = TreeFilter.Apply(new[] { root }, "WM_get");

			Assert.True(any);
			TestNode world = root.Children.Single(c => c.FilterText == "World map");
			Assert.True(world.IsFilterVisible);
			Assert.True(world.IsExpanded);
			Assert.True(world.Children.Single(c => c.FilterText == "wm_get_subtile_state").IsFilterVisible);
			Assert.False(world.Children.Single(c => c.FilterText == "worldmap_xpos").IsFilterVisible);
			Assert.False(root.Children.Single(c => c.FilterText == "Inventory").IsFilterVisible);
		}

		[Fact]
		public void MatchingCategoryShowsAllItsChildren()
		{
			TestNode root = Tree();

			TreeFilter.Apply(new[] { root }, "world map");

			TestNode world = root.Children.Single(c => c.FilterText == "World map");
			Assert.All(world.Children, child => Assert.True(child.IsFilterVisible));
		}

		[Fact]
		public void SeparatorsAreHiddenWhileFiltering()
		{
			TestNode root = Tree();

			TreeFilter.Apply(new[] { root }, "inv");

			Assert.False(root.Children.Single(c => c.HidesWhenFiltering).IsFilterVisible);
		}

		[Fact]
		public void NothingMatchingReturnsFalse()
		{
			Assert.False(TreeFilter.Apply(new[] { Tree() }, "zzz"));
		}

		private static TestNode Tree()
		{
			TestNode world = new TestNode("World map");
			world.Children.Add(new TestNode("wm_get_subtile_state"));
			world.Children.Add(new TestNode("worldmap_xpos"));
			TestNode root = new TestNode("Functions");
			root.Children.Add(world);
			root.Children.Add(new TestNode("Inventory"));
			root.Children.Add(new TestNode("-") { Separator = true });
			return root;
		}

		private static IEnumerable<TestNode> Flatten(TestNode node)
		{
			yield return node;
			foreach (TestNode child in node.Children.SelectMany(Flatten))
			{
				yield return child;
			}
		}

		private sealed class TestNode : IFilterNode
		{
			public TestNode(String text)
			{
				this.FilterText = text;
			}

			public String FilterText { get; }

			public Boolean IsFilterVisible { get; set; } = true;

			public Boolean IsExpanded { get; set; }

			public Boolean Separator { get; init; }

			public Boolean HidesWhenFiltering => this.Separator;

			public List<TestNode> Children { get; } = new List<TestNode>();

			public IEnumerable<IFilterNode> FilterChildren => this.Children;
		}
	}
}
