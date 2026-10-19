namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>One throw-away installation with the real compiler that all the tests of a class share (copying <c>compile.exe</c> for each test would dominate the time).</summary>
	public sealed class RealCompilerFixture : IDisposable
	{
		private readonly RealCompilerWorkspace _workspace = new RealCompilerWorkspace();
		private Int32 _counter;

		/// <summary>Compiles a script text with the real compiler and the built-in preprocessor.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The result.</returns>
		internal async Task<CompilationResult> CompileAsync(String text)
		{
			String name = "case" + Interlocked.Increment(ref this._counter).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl";
			String script = this._workspace.Write(Path.Combine("scripts", name), text);
			return await this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions(), ShortCircuit = false });
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this._workspace.Dispose();
		}
	}
}