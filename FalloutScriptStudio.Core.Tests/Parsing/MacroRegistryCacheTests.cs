namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	/// <summary>The headers of a big mod are read once: the registry keeps its macros while the header files do not change.</summary>
	public class MacroRegistryCacheTests
	{
		private static readonly DateTime _old = DateTime.UtcNow.AddHours(-3);

		private static String WriteOld(TempDirectory dir, String name, String content, DateTime? time = null)
		{
			String path = dir.Write(name, content);
			File.SetLastWriteTimeUtc(path, time ?? _old);
			return path;
		}

		[Fact]
		public void UnchangedHeaders_AreNotReadAgain()
		{
			using TempDirectory dir = new TempDirectory();
			WriteOld(dir, "define.h", "#define GLOBAL_A 1\r\n#define GLOBAL_B 2");
			WriteOld(dir, Path.Combine("sub", "more.h"), "#define GLOBAL_C 3");
			MacroRegistry registry = new MacroRegistry();

			Int32 first = registry.LoadFromDirectory(dir.Path, null, true);
			IReadOnlyDictionary<String, ScriptMacro> snapshot = registry.Macros;
			Int32 second = registry.LoadFromDirectory(dir.Path, null, true);

			Assert.Equal(3, first);
			Assert.Equal(3, second);
			Assert.Same(snapshot, registry.Macros);
		}

		[Fact]
		public void WithoutTheCache_TheHeadersAreAlwaysReadAgain()
		{
			using TempDirectory dir = new TempDirectory();
			WriteOld(dir, "define.h", "#define GLOBAL_A 1");
			MacroRegistry registry = new MacroRegistry();
			registry.LoadFromDirectory(dir.Path, null, true);
			IReadOnlyDictionary<String, ScriptMacro> snapshot = registry.Macros;

			registry.LoadFromDirectory(dir.Path);

			Assert.NotSame(snapshot, registry.Macros);
			Assert.True(registry.Contains("GLOBAL_A"));
		}

		[Fact]
		public void AChangedHeader_IsReadAgain()
		{
			using TempDirectory dir = new TempDirectory();
			String header = WriteOld(dir, "define.h", "#define GLOBAL_A 1");
			MacroRegistry registry = new MacroRegistry();
			registry.LoadFromDirectory(dir.Path, null, true);

			File.WriteAllText(header, "#define GLOBAL_A 1\r\n#define GLOBAL_NEW 2");
			File.SetLastWriteTimeUtc(header, _old.AddMinutes(5));
			Int32 count = registry.LoadFromDirectory(dir.Path, null, true);

			Assert.Equal(2, count);
			Assert.True(registry.Contains("GLOBAL_NEW"));
		}

		[Fact]
		public void AHeaderOfTheSameSizeWithAnotherTimeStamp_IsReadAgain()
		{
			using TempDirectory dir = new TempDirectory();
			String header = WriteOld(dir, "define.h", "#define GLOBAL_A 1");
			MacroRegistry registry = new MacroRegistry();
			registry.LoadFromDirectory(dir.Path, null, true);

			File.WriteAllText(header, "#define GLOBAL_B 1");
			File.SetLastWriteTimeUtc(header, _old.AddSeconds(30));
			registry.LoadFromDirectory(dir.Path, null, true);

			Assert.False(registry.Contains("GLOBAL_A"));
			Assert.True(registry.Contains("GLOBAL_B"));
		}

		[Fact]
		public void AddedAndRemovedHeaders_AreNoticed()
		{
			using TempDirectory dir = new TempDirectory();
			WriteOld(dir, "define.h", "#define GLOBAL_A 1");
			MacroRegistry registry = new MacroRegistry();
			registry.LoadFromDirectory(dir.Path, null, true);

			String added = WriteOld(dir, "added.h", "#define GLOBAL_ADDED 1");
			Assert.Equal(2, registry.LoadFromDirectory(dir.Path, null, true));

			File.Delete(added);
			Assert.Equal(1, registry.LoadFromDirectory(dir.Path, null, true));
			Assert.False(registry.Contains("GLOBAL_ADDED"));
		}

		[Fact]
		public void ARecentlySavedHeader_IsNeverTrustedFromTheCache()
		{
			using TempDirectory dir = new TempDirectory();
			String header = dir.Write("define.h", "#define GLOBAL_A 1");
			MacroRegistry registry = new MacroRegistry();
			registry.LoadFromDirectory(dir.Path, null, true);
			IReadOnlyDictionary<String, ScriptMacro> snapshot = registry.Macros;

			// Same size, same time stamp: only the recency rule can tell that the file was written again.
			DateTime stamp = File.GetLastWriteTimeUtc(header);
			File.WriteAllText(header, "#define GLOBAL_B 1");
			File.SetLastWriteTimeUtc(header, stamp);
			registry.LoadFromDirectory(dir.Path, null, true);

			Assert.NotSame(snapshot, registry.Macros);
			Assert.True(registry.Contains("GLOBAL_B"));
			Assert.False(registry.Contains("GLOBAL_A"));
		}

		[Fact]
		public void TheDirectoryIsPartOfTheCacheKey_AndDiagnosticsStillArrive()
		{
			using TempDirectory first = new TempDirectory();
			using TempDirectory second = new TempDirectory();
			WriteOld(first, "a.h", "#define FIRST 1");
			WriteOld(second, "a.h", "#define SECOND 1");
			MacroRegistry registry = new MacroRegistry();
			ParseDiagnostics diagnostics = new ParseDiagnostics();

			registry.LoadFromDirectory(first.Path, diagnostics, true);
			registry.LoadFromDirectory(second.Path, diagnostics, true);
			Int32 missing = registry.LoadFromDirectory(Path.Combine(first.Path, "nope"), diagnostics, true);

			Assert.True(registry.Contains("SECOND"));
			Assert.False(registry.Contains("FIRST"));
			Assert.Equal(-1, missing);
			Assert.Single(diagnostics);
		}

		[Fact]
		public void ReplaceAndClear_ForgetTheCache()
		{
			using TempDirectory dir = new TempDirectory();
			WriteOld(dir, "define.h", "#define GLOBAL_A 1");
			MacroRegistry registry = new MacroRegistry();
			registry.LoadFromDirectory(dir.Path, null, true);

			registry.Clear();
			Assert.Equal(1, registry.LoadFromDirectory(dir.Path, null, true));
			Assert.True(registry.Contains("GLOBAL_A"));

			registry.Replace(Array.Empty<ScriptMacro>(), dir.Path);
			Assert.Equal(1, registry.LoadFromDirectory(dir.Path, null, true));
			Assert.True(registry.Contains("GLOBAL_A"));
		}
	}
}
