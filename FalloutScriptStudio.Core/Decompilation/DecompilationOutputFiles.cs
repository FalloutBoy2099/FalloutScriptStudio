namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// The file handling every decompiler shares: the work file <c>decomp.ssl</c> in the scratch folder is removed before a run and,
	/// when the run produced it, renamed to the final name afterwards. A file that cannot be deleted or moved (a read-only file, a file
	/// another program holds) gives a failed result, not an exception.
	/// </summary>
	internal static class DecompilationOutputFiles
	{
		private const String LineBreak = "\r\n";

		/// <summary>Creates the scratch folder and removes the work file of an earlier run.</summary>
		/// <param name="paths">The paths of the run.</param>
		/// <param name="options">The compiler options.</param>
		/// <returns><see langword="null"/> when the run can go on, otherwise the failed result to return.</returns>
		public static DecompilationResult? Prepare(CompilerPaths paths, CompilerOptions options)
		{
			try
			{
				Directory.CreateDirectory(options.ScratchDirectory);
				File.Delete(paths.DecompilationFile);
				return null;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Failed(String.Empty, "The scratch folder could not be prepared: " + ex.Message);
			}
		}

		/// <summary>Deletes the work file of a run that was cancelled. A file that cannot be deleted is left; the next run removes it first.</summary>
		/// <param name="paths">The paths of the run.</param>
		public static void Discard(CompilerPaths paths)
		{
			try
			{
				File.Delete(paths.DecompilationFile);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// Nothing to do: Prepare deletes it again before the next run.
			}
		}

		/// <summary>Moves the work file to its final name.</summary>
		/// <param name="paths">The paths of the run.</param>
		/// <param name="request">The request that was run.</param>
		/// <param name="output">The collected output of the decompiler.</param>
		/// <param name="toolSucceeded">Whether the decompiler reported success. A failing int2ssl leaves an empty work file behind, which must not be mistaken for a result.</param>
		/// <returns>A failed result when the decompiler failed or the work file does not exist, otherwise a successful one with the final path.</returns>
		public static DecompilationResult Complete(CompilerPaths paths, DecompilationRequest request, String output, Boolean toolSucceeded = true)
		{
			try
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
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Failed(output, "The decompiled script could not be saved: " + ex.Message);
			}
		}

		/// <summary>Creates the failed result for a file operation that did not work.</summary>
		/// <param name="output">The output collected so far.</param>
		/// <param name="message">What went wrong.</param>
		/// <returns>The failed result; the message is a line of the log in the format of the build log parser.</returns>
		public static DecompilationResult Failed(String output, String message)
		{
			return new DecompilationResult(false, null, output + "[Error] <none>:0: " + message + LineBreak);
		}
	}
}
