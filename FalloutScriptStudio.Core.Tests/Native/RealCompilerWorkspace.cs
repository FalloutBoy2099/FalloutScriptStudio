namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// A throw-away installation layout (<c>resources</c>, <c>scrTemp</c>, <c>scripts</c>) with copies of the real
	/// <c>compile.exe</c> and <c>int2ssl.exe</c> in the resources folder, plus a <see cref="ScriptCompiler"/> that runs real processes.
	/// </summary>
	internal sealed class RealCompilerWorkspace : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public RealCompilerWorkspace()
		{
			if (SslcTools.HasCompiler)
			{
				File.Copy(SslcTools.CompileExe, Path.Combine(this.Resources, "compile.exe"));
			}

			if (SslcTools.HasDecompiler)
			{
				File.Copy(SslcTools.Int2SslExe, Path.Combine(this.Resources, "int2ssl.exe"));
			}

			this.Compiler = new ScriptCompiler(new ProcessRunner(), NullLogger<ScriptCompiler>.Instance);
		}

		public ScriptCompiler Compiler { get; }

		public String Root => this._workspace.Root;

		public String Resources => this._workspace.Resources;

		public String Scratch => this._workspace.Scratch;

		public String Scripts => this._workspace.Scripts;

		/// <summary>Writes a file below the root and returns its full path.</summary>
		public String Write(String relativePath, String content)
		{
			return this._workspace.CreateFile(relativePath, content);
		}

		/// <summary>Creates options for the built-in preprocessor of the real compiler.</summary>
		public CompilerOptions CreateOptions()
		{
			return this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
		}

		public void Dispose()
		{
			this._workspace.Dispose();
		}
	}
}