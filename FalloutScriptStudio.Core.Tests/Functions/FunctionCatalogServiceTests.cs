namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Functions;
	using Xunit;

	public class FunctionCatalogServiceTests : IDisposable
	{
		private const String BuiltIn = "// built-in\n<m>Built-in\nbi<d>Builtin function<s>bi({x});\n";

		private const String User =
			"// user\r\n<m>My macros\r\nDEBUGMSG<d>Debug<s>DEBUGMSG({string})<cr>\r\n<m+>Example\r\nMacros<d>Docs<s>Macros({example})<cr>\r\n<m->\r\n<m>Other\r\n";

		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		private (FunctionCatalogService Service, String BuiltInPath, String UserPath, UserKeywordStore Keywords) Create(String? user = User, String builtIn = BuiltIn)
		{
			String builtInPath = this._folder.Write("descriptions\\functions.ini", builtIn);
			String userPath = this._folder.Combine("descriptions\\userfunctions.ini");
			if (user is not null)
			{
				this._folder.Write("descriptions\\userfunctions.ini", user);
			}

			var keywords = new UserKeywordStore(this._folder.Combine("settings\\user-keywords.json"));
			return (new FunctionCatalogService(new FunctionCatalogPaths(builtInPath, userPath), keywords), builtInPath, userPath, keywords);
		}

		[Fact]
		public async Task LoadAsync_CombinesBuiltInAndUserCategories()
		{
			var (service, _, _, _) = this.Create();
			Int32 changed = 0;
			service.CatalogChanged += (_, _) => changed++;

			FunctionCatalog catalog = await service.LoadAsync();

			Assert.Same(catalog, service.Catalog);
			Assert.Equal(new[] { "Built-in", "My macros", "Other" }, catalog.Roots.Select(r => r.Name).ToArray());
			Assert.False(catalog.Roots[0].IsUser);
			Assert.True(catalog.Roots[1].IsUser);
			Assert.Equal(1, changed);
			Assert.Empty(catalog.Issues);
		}

		[Fact]
		public async Task LoadAsync_MissingUserFile_IsCreatedFromTheTemplate()
		{
			var (service, _, userPath, _) = this.Create(user: null);

			FunctionCatalog catalog = await service.LoadAsync();

			Assert.True(File.Exists(userPath));
			Assert.Equal(FunctionCatalogService.UserFileTemplate, File.ReadAllText(userPath));
			Assert.Contains(catalog.Roots, r => r.Name == "User defined macros");
		}

		[Fact]
		public async Task LoadAsync_MissingBuiltInFile_ReportsAnIssue()
		{
			var (service, builtInPath, _, _) = this.Create();
			File.Delete(builtInPath);

			FunctionCatalog catalog = await service.LoadAsync();

			Assert.Contains(catalog.Issues, i => i.Source == FunctionSource.BuiltIn && i.Line == 0);
			Assert.Equal(new[] { "My macros", "Other" }, catalog.Roots.Select(r => r.Name).ToArray());
		}

		[Fact]
		public async Task AddFunction_ToCategory_InsertsAsFirstItem()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			FunctionOperationResult result = await service.AddFunctionAsync(
				catalog.Roots[1],
				new FunctionDefinition("MyFunc", "Does things", "MyFunc({a});\r\n"));

			Assert.True(result.Succeeded);
			String[] lines = File.ReadAllLines(userPath);
			Assert.Equal("<m>My macros", lines[1]);
			Assert.Equal("MyFunc<d>Does things<s>MyFunc({a});", lines[2]);
			Assert.Equal("DEBUGMSG<d>Debug<s>DEBUGMSG({string})<cr>", lines[3]);
			Assert.Equal("MyFunc", service.Catalog.Roots[1].Children[0].Name);
		}

		[Fact]
		public async Task AddFunction_AfterFunction_InsertsRightAfterIt_AndConvertsLineBreaks()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			FunctionNode debug = catalog.FindFunctions("DEBUGMSG").Single();

			FunctionOperationResult result = await service.AddFunctionAsync(
				debug,
				new FunctionDefinition("Multi", "Two lines\r\nof description", "line1\r\nline2\nline3"));

			Assert.True(result.Succeeded);
			Assert.Equal("Multi<d>Two lines of description<s>line1<cr>line2<cr>line3", File.ReadAllLines(userPath)[3]);
			FunctionDefinition read = FunctionDefinition.FromNode(service.Catalog.FindFunctions("Multi").Single());
			Assert.Equal("Two lines of description", read.Description);
			Assert.Equal("line1" + Environment.NewLine + "line2" + Environment.NewLine + "line3", read.Code);
		}

		[Fact]
		public async Task AddFunction_WithHighlight_AddsTheKeyword_AndDeleteRemovesIt()
		{
			var (service, _, _, keywords) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			await service.AddFunctionAsync(catalog.Roots[1], new FunctionDefinition("MyMacro", "d", "MY_MACRO({x})"), highlightKeyword: true);

			Assert.Equal(new[] { "MY_MACRO" }, keywords.Keywords.ToArray());

			FunctionNode added = service.Catalog.FindFunctions("MyMacro").Single();
			FunctionOperationResult deleted = await service.DeleteAsync(added);

			Assert.True(deleted.Succeeded);
			Assert.Empty(keywords.Keywords);
			Assert.Empty(service.Catalog.FindFunctions("MyMacro"));
		}

		[Fact]
		public async Task AddFunction_BuiltInTarget_IsNotAllowed()
		{
			var (service, _, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			FunctionOperationResult result = await service.AddFunctionAsync(catalog.Roots[0], new FunctionDefinition("x", "d", "c"));

			Assert.Equal(FunctionOperationError.NotAllowed, result.Error);
		}

		[Theory]
		[InlineData("", "d", "c")]
		[InlineData("a<d>b", "d", "c")]
		[InlineData("a", "x<s>y", "c")]
		public async Task AddFunction_InvalidDefinition_IsRejected(String name, String description, String code)
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			String before = File.ReadAllText(userPath);

			FunctionOperationResult result = await service.AddFunctionAsync(catalog.Roots[1], new FunctionDefinition(name, description, code));

			Assert.Equal(FunctionOperationError.InvalidDefinition, result.Error);
			Assert.Equal(before, File.ReadAllText(userPath));
		}

		[Fact]
		public async Task EditFunction_UserFunction_ReplacesTheLine()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			FunctionOperationResult result = await service.EditFunctionAsync(
				catalog.FindFunctions("Macros").Single(),
				new FunctionDefinition("Macros2", "New docs", "Macros2({x})<cr>"));

			Assert.True(result.Succeeded);
			Assert.Equal("Macros2<d>New docs<s>Macros2({x})<cr>", File.ReadAllLines(userPath)[4]);
		}

		[Fact]
		public async Task EditFunction_BuiltInFunction_EditsTheBuiltInFile_AndKeepsLfLineEndings()
		{
			var (service, builtInPath, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			FunctionOperationResult result = await service.EditFunctionAsync(catalog.FindFunctions("bi").Single(), new FunctionDefinition("bi", "Changed", "bi({y});"));

			Assert.True(result.Succeeded);
			Assert.Equal("// built-in\n<m>Built-in\nbi<d>Changed<s>bi({y});\n", File.ReadAllText(builtInPath));
		}

		[Fact]
		public async Task EditFunction_OnACategory_IsNotAllowed()
		{
			var (service, _, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			FunctionOperationResult result = await service.EditFunctionAsync(catalog.Roots[1], new FunctionDefinition("x", "d", "c"));

			Assert.Equal(FunctionOperationError.NotAllowed, result.Error);
		}

		[Fact]
		public async Task AddCategory_ToTopCategory_AppendsAfterItsLastChild()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			FunctionOperationResult result = await service.AddCategoryAsync(catalog.Roots[1], "Fresh");

			Assert.True(result.Succeeded);
			String[] lines = File.ReadAllLines(userPath);
			Assert.Equal(new[] { "<m->", "<m+>Fresh", "<m->", "<m>Other" }, lines.Skip(5).Take(4).ToArray());
			FunctionNode fresh = service.Catalog.Roots[1].Children.Last();
			Assert.Equal("Fresh", fresh.Name);
			Assert.Empty(fresh.Children);
		}

		[Fact]
		public async Task AddCategory_ToSubCategory_NestsInside()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			FunctionNode example = catalog.Roots[1].Children[1];

			FunctionOperationResult result = await service.AddCategoryAsync(example, "Deep");

			Assert.True(result.Succeeded);
			Assert.Equal(new[] { "<m+>Example", "Macros<d>Docs<s>Macros({example})<cr>", "<m+>Deep", "<m->", "<m->" }, File.ReadAllLines(userPath).Skip(3).Take(5).ToArray());
			Assert.Equal("Deep", service.Catalog.Roots[1].Children[1].Children.Last().Name);
		}

		[Fact]
		public async Task AddCategory_TooDeepOrBuiltIn_IsNotAllowed()
		{
			var (service, _, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			await service.AddCategoryAsync(catalog.Roots[1].Children[1], "Deep");
			FunctionNode deep = service.Catalog.Roots[1].Children[1].Children.Last();

			Assert.False(service.CanAddCategory(deep));
			Assert.Equal(FunctionOperationError.NotAllowed, (await service.AddCategoryAsync(deep, "Deeper")).Error);
			Assert.Equal(FunctionOperationError.NotAllowed, (await service.AddCategoryAsync(service.Catalog.Roots[0], "x")).Error);
		}

		[Fact]
		public async Task AddCategory_WithoutParent_AppendsATopLevelCategory()
		{
			var (service, _, userPath, _) = this.Create();
			await service.LoadAsync();

			FunctionOperationResult result = await service.AddCategoryAsync(null, "Brand new");

			Assert.True(result.Succeeded);
			String text = File.ReadAllText(userPath);
			Assert.EndsWith("<m>Other\r\n<m>Brand new\r\n", text.Replace("\r\n\r\n", "\r\n"));
			Assert.Equal("Brand new", service.Catalog.Roots.Last().Name);
		}

		[Fact]
		public async Task AddCategory_EmptyTitle_IsRejected()
		{
			var (service, _, _, _) = this.Create();
			await service.LoadAsync();

			Assert.Equal(FunctionOperationError.InvalidDefinition, (await service.AddCategoryAsync(null, "  ")).Error);
		}

		[Fact]
		public async Task RenameCategory_ChangesTheTitle_KeepingTheMarker()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			Assert.True((await service.RenameCategoryAsync(catalog.Roots[1].Children[1], "Renamed sub")).Succeeded);
			Assert.True((await service.RenameCategoryAsync(service.Catalog.Roots[1], "Renamed top")).Succeeded);

			String[] lines = File.ReadAllLines(userPath);
			Assert.Equal("<m>Renamed top", lines[1]);
			Assert.Equal("<m+>Renamed sub", lines[3]);
		}

		[Fact]
		public async Task RenameCategory_BuiltIn_IsNotAllowed()
		{
			var (service, _, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			Assert.Equal(FunctionOperationError.NotAllowed, (await service.RenameCategoryAsync(catalog.Roots[0], "x")).Error);
		}

		[Fact]
		public async Task Delete_EmptySubCategory_RemovesHeaderAndClosingMarker()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			await service.AddCategoryAsync(catalog.Roots[1], "Temp");
			FunctionNode temp = service.Catalog.Roots[1].Children.Last();

			FunctionOperationResult result = await service.DeleteAsync(temp);

			Assert.True(result.Succeeded);
			Assert.Equal(User, File.ReadAllText(userPath));
		}

		[Fact]
		public async Task Delete_RulesFollowTheLegacyMenu()
		{
			var (service, _, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			FunctionNode top = catalog.Roots[1];
			FunctionNode example = top.Children[1];

			Assert.False(service.CanDelete(top));                     // top level node
			Assert.False(service.CanDelete(example));                 // not empty
			Assert.True(service.CanDelete(example.Children[0]));      // a user function
			Assert.False(service.CanDelete(catalog.FindFunctions("bi").Single()));  // built-in
			Assert.Equal(FunctionOperationError.NotAllowed, (await service.DeleteAsync(top)).Error);
		}

		[Fact]
		public async Task Operations_WhenTheFileChanged_ReportNodeChanged()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			FunctionNode debug = catalog.FindFunctions("DEBUGMSG").Single();
			File.WriteAllText(userPath, "// shifted\r\n" + User);
			String changed = File.ReadAllText(userPath);

			FunctionOperationResult edit = await service.EditFunctionAsync(debug, new FunctionDefinition("x", "d", "c"));
			FunctionOperationResult delete = await service.DeleteAsync(debug);

			Assert.Equal(FunctionOperationError.NodeChanged, edit.Error);
			Assert.Equal(FunctionOperationError.NodeChanged, delete.Error);
			Assert.Equal(changed, File.ReadAllText(userPath));
		}

		[Fact]
		public async Task Operations_LeaveNoTemporaryFilesAndKeepCrLf()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();

			await service.AddFunctionAsync(catalog.Roots[1], new FunctionDefinition("A", "d", "A"));

			Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(userPath)!, "*.tmp"));
			String text = File.ReadAllText(userPath);
			Assert.DoesNotContain("\n", text.Replace("\r\n", String.Empty));
		}

		[Fact]
		public async Task Operations_IoFailure_ReturnsIoError()
		{
			var (service, builtInPath, _, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			FunctionNode bi = catalog.FindFunctions("bi").Single();
			File.Delete(builtInPath);

			FunctionOperationResult result = await service.EditFunctionAsync(bi, new FunctionDefinition("bi", "d", "c"));

			Assert.Equal(FunctionOperationError.IoError, result.Error);
		}

		[Fact]
		public async Task ConcurrentEdits_AreSerialized()
		{
			var (service, _, userPath, _) = this.Create();
			FunctionCatalog catalog = await service.LoadAsync();
			FunctionNode target = catalog.Roots[1];

			FunctionOperationResult[] results = await Task.WhenAll(
				Enumerable.Range(0, 8).Select(i => service.AddCategoryAsync(null, "Cat" + i)));

			Assert.All(results, r => Assert.True(r.Succeeded));
			Assert.Equal(8, File.ReadAllLines(userPath).Count(l => l.StartsWith("<m>Cat", StringComparison.Ordinal)));
			Assert.NotNull(target);
		}
	}
}
