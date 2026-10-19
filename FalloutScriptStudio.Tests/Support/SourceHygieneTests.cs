namespace FalloutScriptStudio.Tests.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// Keeps the sources in the shape the <c>.editorconfig</c> asks for (CRLF line endings, no byte order mark, no final newline) and free of
	/// the C# preview syntax <c>[with(...)]</c> (collection expression arguments), which the released compilers of Visual Studio reject (CS8652).
	/// </summary>
	public sealed class SourceHygieneTests
	{
		private static readonly String[] Extensions = { ".cs", ".xaml", ".csproj", ".props", ".targets", ".md", ".json", ".ps1", ".manifest", ".xshd", ".py" };

		private static readonly HashSet<String> SkippedFolders = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
		{
			"bin", "obj", ".vs", ".git", "publish", "publish.tmp", "_test_files", "_icons",
		};

		private static readonly Regex CollectionArguments = new Regex(@"\[\s*with\s*\(", RegexOptions.Compiled);

		private static IEnumerable<String> SourceFiles()
		{
			Stack<String> pending = new Stack<String>();
			pending.Push(TestFiles.RepositoryRoot);
			while (pending.Count > 0)
			{
				String folder = pending.Pop();
				foreach (String directory in Directory.GetDirectories(folder))
				{
					if (!SkippedFolders.Contains(Path.GetFileName(directory)))
					{
						pending.Push(directory);
					}
				}

				foreach (String file in Directory.GetFiles(folder))
				{
					if (Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
					{
						yield return file;
					}
				}
			}
		}

		private static String Relative(String file)
		{
			return Path.GetRelativePath(TestFiles.RepositoryRoot, file);
		}

		[Fact]
		public void No_source_file_ends_with_a_newline()
		{
			List<String> offenders = SourceFiles()
				.Where(file =>
				{
					Byte[] bytes = File.ReadAllBytes(file);
					return bytes.Length > 0 && (bytes[^1] == (Byte)'\n' || bytes[^1] == (Byte)'\r');
				})
				.Select(Relative)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			Assert.True(offenders.Count == 0, "The .editorconfig sets insert_final_newline = false. Files ending with a newline (run the strip script or remove the last line break): " + String.Join(", ", offenders.Take(30)));
		}

		[Fact]
		public void Every_line_break_of_a_source_file_is_a_carriage_return_and_a_line_feed()
		{
			List<String> offenders = new List<String>();
			foreach (String file in SourceFiles())
			{
				Byte[] bytes = File.ReadAllBytes(file);
				for (Int32 i = 0; i < bytes.Length; i++)
				{
					Boolean lineFeedAlone = bytes[i] == (Byte)'\n' && (i == 0 || bytes[i - 1] != (Byte)'\r');
					Boolean carriageReturnAlone = bytes[i] == (Byte)'\r' && (i + 1 >= bytes.Length || bytes[i + 1] != (Byte)'\n');
					if (lineFeedAlone || carriageReturnAlone)
					{
						offenders.Add(Relative(file));
						break;
					}
				}
			}

			Assert.True(offenders.Count == 0, "The .editorconfig sets end_of_line = crlf. Files with other line breaks: " + String.Join(", ", offenders.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Take(30)));
		}

		[Fact]
		public void No_code_or_markup_file_starts_with_a_byte_order_mark()
		{
			List<String> offenders = SourceFiles()
				.Where(file => !file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
				.Where(file =>
				{
					Byte[] head = new Byte[3];
					using FileStream stream = File.OpenRead(file);
					return stream.Read(head, 0, 3) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
				})
				.Select(Relative)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			Assert.True(offenders.Count == 0, "The .editorconfig sets charset = utf-8 (no BOM). Files with a BOM: " + String.Join(", ", offenders.Take(30)));
		}

		[Fact]
		public void No_source_uses_the_preview_collection_expression_arguments()
		{
			List<String> offenders = SourceFiles()
				.Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
				.Where(file => CollectionArguments.IsMatch(File.ReadAllText(file)))
				.Where(file => !file.EndsWith("SourceHygieneTests.cs", StringComparison.OrdinalIgnoreCase))
				.Select(Relative)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			Assert.True(offenders.Count == 0, "[with(...)] (collection expression arguments) is a C# preview feature that Visual Studio rejects with CS8652: use new(...) or new(...) { ... }. Files: " + String.Join(", ", offenders.Take(30)));
		}
	}
}