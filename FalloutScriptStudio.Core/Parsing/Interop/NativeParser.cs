namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;

	/// <summary>
	/// The native parser: calls the 32-bit <c>parser.dll</c> (the SSLC front end) to obtain procedures, variables and references
	/// (legacy <c>ParserExternal</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The library is optional. When it is not configured, missing, or cannot be loaded (for example in a 64-bit process)
	/// the result has <see cref="NativeParseStatus.Unavailable"/> and the caller falls back to the internal parser.
	/// </para>
	/// <para>
	/// The native code keeps global state and works through one shared scratch file, so all calls are serialized with a
	/// process-wide <see cref="SemaphoreSlim"/>.
	/// </para>
	/// <para>
	/// The parser works in the process that calls it, and an access violation inside <c>parser.dll</c> cannot be caught on .NET
	/// (Core): it terminates the process. The application therefore never uses this class directly. It runs inside
	/// <c>FalloutScriptStudio.ParserHost.exe</c> and the application talks to that process through
	/// <see cref="OutOfProcessNativeParser"/>. Only ordinary managed exceptions (marshalling errors, invalid data) are handled
	/// here and reported as <see cref="NativeParseStatus.Failed"/>.
	/// </para>
	/// </remarks>
	public sealed class NativeParser : INativeParser, IDisposable
	{
		// The native library is process-global, therefore the gate has to be process-global as well.
		private static readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		private NativeParserLibrary? _library;
		private String? _libraryPath;
		private String? _failedPath;
		private String? _failure;
		private DateTime _failedStamp;

		/// <inheritdoc />
		public async Task<NativeParseResult> ParseAsync(String scriptText, String filePath, ParserOptions options, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (String.IsNullOrWhiteSpace(options.NativeParserPath))
			{
				return Unavailable("The path of the native parser (parser.dll) is not configured.");
			}

			String libraryPath;
			try
			{
				libraryPath = Path.GetFullPath(options.NativeParserPath);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return Unavailable("The path of the native parser is invalid: " + ex.Message);
			}

			if (!File.Exists(libraryPath))
			{
				return Unavailable("The native parser was not found: '" + libraryPath + "'.");
			}

			await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				return await Task.Run(() => this.ParseCore(scriptText, filePath, options, libraryPath), CancellationToken.None).ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}
		}

		/// <summary>Unloads the native library.</summary>
		public void Dispose()
		{
			this._library?.Dispose();
			this._library = null;
			this._libraryPath = null;
		}

		// parser.dll treats an empty string differently from "not set": an empty list of definitions makes the preprocessor fail with
		// "Not an identifier" (status 2), although the settings produce an empty string when the option is blank.
		private static String? NullIfBlank(String? value)
		{
			return String.IsNullOrWhiteSpace(value) ? null : value;
		}

		private static NativeParseResult Unavailable(String message)
		{
			return new NativeParseResult(NativeParseStatus.Unavailable) { Message = message };
		}

		private static String ReadNativeLog(ParserOptions options)
		{
			String path = options.NativeLogPath ?? Path.Combine(Environment.CurrentDirectory, ParserOptions.DefaultNativeLogFileName);
			try
			{
				if (!File.Exists(path))
				{
					return String.Empty;
				}

				String log;
				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				using (var reader = new StreamReader(stream, TextEncodings.Ansi))
				{
					log = reader.ReadToEnd();
				}

				try
				{
					File.Delete(path);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// The native parser may keep the file open after an internal error; the next parse overwrites it.
				}

				return log;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return String.Empty;
			}
		}

		private NativeParseResult ParseCore(String scriptText, String filePath, ParserOptions options, String libraryPath)
		{
			NativeParserLibrary? library = this.EnsureLibrary(libraryPath, out String? failure);
			if (library is null)
			{
				return Unavailable(failure ?? "The native parser could not be loaded.");
			}

			String scratchPath;
			try
			{
				String scratchDirectory = options.ScratchDirectory ?? Path.Combine(Path.GetTempPath(), "FalloutScriptStudio");
				Directory.CreateDirectory(scratchDirectory);
				scratchPath = Path.Combine(scratchDirectory, options.ScratchFileName);
				File.WriteAllText(scratchPath, scriptText, TextEncodings.Ansi);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return new NativeParseResult(NativeParseStatus.Failed) { Message = "Cannot write the scratch file of the native parser: " + ex.Message };
			}

			// A log left by an earlier run must not be mistaken for the log of this one.
			_ = ReadNativeLog(options);

			Int32 exitCode;
			try
			{
				exitCode = library.ParseMain(
					scratchPath,
					filePath,
					Path.GetDirectoryName(filePath) ?? String.Empty,
					NullIfBlank(options.PreprocessorDefinitions),
					NullIfBlank(options.HeadersDirectory),
					options.BackwardCompatibilityMode);
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				return new NativeParseResult(NativeParseStatus.Failed)
				{
					Message = "An unexpected error occurred while parsing the script with parser.dll: " + ex.Message,
					Log = ReadNativeLog(options),
				};
			}

			String log = ReadNativeLog(options);
			if (exitCode > 0)
			{
				// Syntax or preprocessor error: the library holds no usable data, and reading it would crash the process.
				return new NativeParseResult(NativeParseStatus.Completed) { ExitCode = exitCode, Log = log };
			}

			try
			{
				NativeDataReader.Read(library, SslSyntax.SplitLines(scriptText), out List<ScriptProcedure> procedures, out List<ScriptVariable> variables);
				return new NativeParseResult(NativeParseStatus.Completed)
				{
					ExitCode = exitCode,
					Procedures = procedures,
					Variables = variables,
					Log = log,
				};
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				return new NativeParseResult(NativeParseStatus.Failed)
				{
					Message = "The data returned by parser.dll could not be read: " + ex.Message,
					Log = log,
				};
			}
		}

		private NativeParserLibrary? EnsureLibrary(String libraryPath, out String? failure)
		{
			failure = null;
			if (this._library is not null && String.Equals(this._libraryPath, libraryPath, StringComparison.OrdinalIgnoreCase))
			{
				return this._library;
			}

			this._library?.Dispose();
			this._library = null;
			this._libraryPath = null;

			DateTime stamp = File.GetLastWriteTimeUtc(libraryPath);
			if (String.Equals(this._failedPath, libraryPath, StringComparison.OrdinalIgnoreCase) && this._failedStamp == stamp)
			{
				failure = this._failure;
				return null;
			}

			NativeParserLibrary? loaded = NativeParserLibrary.TryLoad(libraryPath, out String? loadFailure);
			if (loaded is null)
			{
				this._failedPath = libraryPath;
				this._failedStamp = stamp;
				this._failure = "The native parser could not be loaded (" + loadFailure + "). It is a 32-bit library: run the application as a 32-bit process.";
				failure = this._failure;
				return null;
			}

			this._failedPath = null;
			this._failure = null;
			this._library = loaded;
			this._libraryPath = libraryPath;
			return loaded;
		}
	}
}
