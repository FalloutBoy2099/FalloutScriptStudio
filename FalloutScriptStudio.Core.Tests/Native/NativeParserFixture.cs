namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using FalloutScriptStudio.Core.Tests.Parsing;

	/// <summary>
	/// Shared state of the integration tests that run the real <c>parser.dll</c> through the real parser host: a temporary folder with
	/// a copy of the library and one <see cref="OutOfProcessNativeParser"/> (so that many tests reuse one host process).
	/// </summary>
	public sealed class NativeParserFixture : IDisposable
	{
		private readonly TempDirectory _directory = new TempDirectory();

		public NativeParserFixture()
		{
			this.ScriptsDirectory = Path.Combine(this._directory.Path, "scripts");
			this.HeadersDirectory = Path.Combine(this._directory.Path, "headers");
			Directory.CreateDirectory(this.ScriptsDirectory);
			Directory.CreateDirectory(this.HeadersDirectory);

			if (SslcTools.HasParser)
			{
				String resources = Path.Combine(this._directory.Path, "resources");
				Directory.CreateDirectory(resources);
				this.ParserDllPath = Path.Combine(resources, "parser.dll");
				File.Copy(SslcTools.ParserDll, this.ParserDllPath);
				this.Parser = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = SslcTools.HostExe });
			}
		}

		/// <summary>Gets the parser under test, or <c>null</c> when the real tools are missing.</summary>
		public OutOfProcessNativeParser? Parser { get; }

		/// <summary>Gets the folder where the test scripts are written.</summary>
		public String ScriptsDirectory { get; }

		/// <summary>Gets a folder meant to be passed as the include search folder.</summary>
		public String HeadersDirectory { get; }

		/// <summary>Gets the copy of <c>parser.dll</c> the tests use.</summary>
		public String ParserDllPath { get; } = String.Empty;

		/// <summary>Gets the folder used for the scratch file of the parser.</summary>
		public String ScratchDirectory => Path.Combine(this._directory.Path, "scrTemp");

		/// <summary>Creates parser options that point to the copy of <c>parser.dll</c>.</summary>
		public ParserOptions CreateOptions(String? preprocessorDefinitions = null, String? headersDirectory = null, Int32 backwardMode = 0)
		{
			return new ParserOptions
			{
				NativeParserPath = this.ParserDllPath,
				ScratchDirectory = this.ScratchDirectory,
				PreprocessorDefinitions = preprocessorDefinitions,
				HeadersDirectory = headersDirectory,
				BackwardCompatibilityMode = backwardMode,
			};
		}

		/// <summary>Writes a file below the temporary folder and returns its full path.</summary>
		public String Write(String relativePath, String content)
		{
			return this._directory.Write(relativePath, content);
		}

		/// <summary>Copies a folder (recursively) below the temporary folder and returns the full path of the copy.</summary>
		public String CopyDirectory(String source, String relativeTarget)
		{
			String target = Path.Combine(this._directory.Path, relativeTarget);
			foreach (String file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
			{
				String destination = Path.Combine(target, Path.GetRelativePath(source, file));
				Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
				File.Copy(file, destination, true);
			}

			return target;
		}

		/// <summary>Writes a script into <see cref="ScriptsDirectory"/> and parses it.</summary>
		public async Task<NativeParseResult> ParseAsync(String fileName, String text, ParserOptions? options = null)
		{
			String path = this.Write(Path.Combine("scripts", fileName), text);
			return await this.ParseFileAsync(path, text, options).ConfigureAwait(false);
		}

		/// <summary>Parses text as if it was the content of an existing file.</summary>
		public Task<NativeParseResult> ParseFileAsync(String path, String text, ParserOptions? options = null)
		{
			OutOfProcessNativeParser parser = this.Parser ?? throw new InvalidOperationException("The real tools are not available.");
			return parser.ParseAsync(text, path, options ?? this.CreateOptions(), CancellationToken.None);
		}

		public void Dispose()
		{
			this.Parser?.Dispose();
			this._directory.Dispose();
		}
	}
}
