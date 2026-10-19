namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Tests.Parsing;
	using Xunit;

	/// <summary>The "search includes" option is evaluated in one place (<see cref="IncludeSearch"/>) and every consumer follows it.</summary>
	public sealed class IncludeSearchTests
	{
		[Theory]
		[InlineData(true, @"C:\h\scripts.h", true)]
		[InlineData(false, @"C:\h\scripts.h", false)]
		[InlineData(true, null, false)]
		[InlineData(true, "", false)]
		[InlineData(true, "   ", false)]
		[InlineData(false, null, false)]
		public void IsEnabled_NeedsTheOptionAndAScriptsHeaderFile(Boolean option, String? scriptsHeader, Boolean expected)
		{
			Assert.Equal(expected, IncludeSearch.IsEnabled(option, scriptsHeader));
		}

		[Fact]
		public void GetHeadersDirectory_IsNullWhenDisabledOrWhenNoFolderIsConfigured()
		{
			Assert.Equal(@"C:\h", IncludeSearch.GetHeadersDirectory(true, @"C:\h\scripts.h", @"C:\h"));
			Assert.Null(IncludeSearch.GetHeadersDirectory(false, @"C:\h\scripts.h", @"C:\h"));
			Assert.Null(IncludeSearch.GetHeadersDirectory(true, null, @"C:\h"));
			Assert.Null(IncludeSearch.GetHeadersDirectory(true, @"C:\h\scripts.h", null));
			Assert.Null(IncludeSearch.GetHeadersDirectory(true, @"C:\h\scripts.h", "  "));
		}

		[Fact]
		public void CompilerOptions_AgreeWithTheSharedRule()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(@"C:\Editor");
			AppSettings settings = CreateSettings(true);

			Assert.True(settings.ToCompilerOptions(paths).SearchesIncludes);

			settings.Paths.ScriptsHeaderFile = "  ";
			Assert.False(settings.ToCompilerOptions(paths).SearchesIncludes);

			settings.Paths.ScriptsHeaderFile = @"D:\h\scripts.h";
			settings.Compiler.SearchIncludePath = false;
			Assert.False(settings.ToCompilerOptions(paths).SearchesIncludes);
		}

		[Fact]
		public void GetIncludeHeadersDirectory_FollowsTheSettings()
		{
			AppSettings settings = CreateSettings(true);
			Assert.Equal(@"D:\h", settings.GetIncludeHeadersDirectory());

			settings.Compiler.SearchIncludePath = false;
			Assert.Null(settings.GetIncludeHeadersDirectory());

			settings.Compiler.SearchIncludePath = true;
			settings.Paths.ScriptsHeaderFile = null;
			Assert.Null(settings.GetIncludeHeadersDirectory());
		}

		[Fact]
		public void ToIncludeResolver_FindsHeadersOnlyWhileTheSearchIsEnabled()
		{
			using TempDirectory root = new TempDirectory();
			String headers = Path.GetDirectoryName(root.Write(Path.Combine("headers", "shared.h"), String.Empty))!;
			String scripts = Path.Combine(root.Path, "scripts");
			AppPaths paths = AppPaths.FromBaseDirectory(Path.Combine(root.Path, "app"));
			AppSettings settings = new AppSettings();
			settings.Paths.HeadersDirectory = headers;
			settings.Paths.ScriptsHeaderFile = Path.Combine(headers, "scripts.h");

			settings.Compiler.SearchIncludePath = true;
			Assert.Equal(Path.Combine(headers, "shared.h"), settings.ToIncludeResolver(paths).Resolve("shared.h", scripts));

			settings.Compiler.SearchIncludePath = false;
			Assert.Equal(Path.Combine(scripts, "shared.h"), settings.ToIncludeResolver(paths).Resolve("shared.h", scripts));

			settings.Compiler.SearchIncludePath = true;
			settings.Paths.ScriptsHeaderFile = null;
			Assert.Equal(Path.Combine(scripts, "shared.h"), settings.ToIncludeResolver(paths).Resolve("shared.h", scripts));
		}

		[Fact]
		public void ToIncludeResolver_StillSearchesTheProgramFolder()
		{
			using TempDirectory root = new TempDirectory();
			String program = Path.Combine(root.Path, "app");
			root.Write(Path.Combine("app", "app.h"), String.Empty);
			AppSettings settings = CreateSettings(false);

			String resolved = settings.ToIncludeResolver(AppPaths.FromBaseDirectory(program)).Resolve("app.h", Path.Combine(root.Path, "scripts"));

			Assert.Equal(Path.Combine(program, "app.h"), resolved);
		}

		private static AppSettings CreateSettings(Boolean searchIncludes)
		{
			AppSettings settings = new AppSettings();
			settings.Compiler.SearchIncludePath = searchIncludes;
			settings.Paths.HeadersDirectory = @"D:\h";
			settings.Paths.ScriptsHeaderFile = @"D:\h\scripts.h";
			return settings;
		}
	}
}