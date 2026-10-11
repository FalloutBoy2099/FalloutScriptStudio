namespace FalloutScriptStudio.Tests.Logging
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Microsoft.Extensions.Logging;
	using Xunit;

	public sealed class FileLoggerProviderTests
	{
		[Fact]
		public void Lines_are_appended_to_the_log_file()
		{
			using TempFolder folder = new TempFolder();
			String path = folder.Combine("sse.log");
			using FileLoggerProvider provider = new FileLoggerProvider(path);

			provider.Write("first");
			provider.Write("second");

			String[] lines = File.ReadAllLines(path);
			Assert.Equal(2, lines.Length);
			Assert.EndsWith("first", lines[0], StringComparison.Ordinal);
			Assert.EndsWith("second", lines[1], StringComparison.Ordinal);
			Assert.False(File.Exists(provider.RotatedPath));
		}

		[Fact]
		public void The_log_is_rotated_to_a_dot_one_copy_when_it_grows_over_the_limit()
		{
			using TempFolder folder = new TempFolder();
			String path = folder.Combine("sse.log");
			using FileLoggerProvider provider = new FileLoggerProvider(path, 200);

			provider.Write(new String('a', 250));
			Assert.False(File.Exists(provider.RotatedPath));

			provider.Write("after the rotation");

			Assert.Equal(path + ".1", provider.RotatedPath);
			Assert.Contains(new String('a', 250), File.ReadAllText(provider.RotatedPath), StringComparison.Ordinal);
			String current = File.ReadAllText(path);
			Assert.Contains("after the rotation", current, StringComparison.Ordinal);
			Assert.DoesNotContain("aaaa", current, StringComparison.Ordinal);
		}

		[Fact]
		public void A_second_rotation_replaces_the_previous_copy()
		{
			using TempFolder folder = new TempFolder();
			String path = folder.Combine("sse.log");
			using FileLoggerProvider provider = new FileLoggerProvider(path, 100);

			provider.Write(new String('a', 150));
			provider.Write(new String('b', 150));
			provider.Write("last");

			String rotated = File.ReadAllText(provider.RotatedPath);
			Assert.Contains("bbbb", rotated, StringComparison.Ordinal);
			Assert.DoesNotContain("aaaa", rotated, StringComparison.Ordinal);
			Assert.Contains("last", File.ReadAllText(path), StringComparison.Ordinal);
		}

		[Fact]
		public void The_default_limit_is_one_megabyte()
		{
			Assert.Equal(1024 * 1024, FileLoggerProvider.DefaultMaxFileBytes);
		}

		[Fact]
		public void Loggers_write_information_and_above_with_their_category()
		{
			using TempFolder folder = new TempFolder();
			String path = folder.Combine("sse.log");
			using FileLoggerProvider provider = new FileLoggerProvider(path);
			ILogger logger = provider.CreateLogger("Test.Category");

			logger.LogDebug("hidden");
			logger.LogWarning("visible");

			String text = File.ReadAllText(path);
			Assert.DoesNotContain("hidden", text, StringComparison.Ordinal);
			Assert.Contains("[Warning] Test.Category: visible", text, StringComparison.Ordinal);
		}
	}
}
