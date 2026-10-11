namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// The file handling every decompiler shares: the work file <c>decomp.ssl</c> in the scratch folder is removed before a run and,
	/// when the run produced it, renamed to the final name afterwards.
	/// </summary>
	internal static class DecompilationOutputFiles
	{
		/// <summary>Creates the scratch folder and removes the work file of an earlier run.</summary>
		/// <param name="paths">The paths of the run.</param>
		/// <param name="options">The compiler options.</param>
		public static void Prepare(CompilerPaths paths, CompilerOptions options)
		{
			Directory.CreateDirectory(options.ScratchDirectory);
			File.Delete(paths.DecompilationFile);
		}

		/// <summary>Moves the work file to its final name.</summary>
		/// <param name="paths">The paths of the run.</param>
		/// <param name="request">The request that was run.</param>
		/// <param name="output">The collected output of the decompiler.</param>
		/// <param name="toolSucceeded">Whether the decompiler reported success. A failing int2ssl leaves an empty work file behind, which must not be mistaken for a result.</param>
		/// <returns>A failed result when the decompiler failed or the work file does not exist, otherwise a successful one with the final path.</returns>
		public static DecompilationResult Complete(CompilerPaths paths, DecompilationRequest request, String output, Boolean toolSucceeded = true)
		{
			if (!toolSucceeded)
			{
				File.Delete(paths.DecompilationFile);
				return new DecompilationResult(false, null, output);
			}

			if (!File.Exists(paths.DecompilationFile))
			{
				return new DecompilationResult(false, null, output);
			}

			String resultPath = paths.GetDecompiledPath(request.ScriptPath, request.TemporaryOutput);
			File.Delete(resultPath);
			File.Move(paths.DecompilationFile, resultPath);
			return new DecompilationResult(true, resultPath, output);
		}
	}
}
