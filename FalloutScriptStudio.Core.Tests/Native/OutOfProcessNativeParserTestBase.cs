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

	/// <summary>Shared fixture of the <see cref="OutOfProcessNativeParser"/> tests: simulated hosts, a temporary folder and the parser under test.</summary>
	public abstract class OutOfProcessNativeParserTestBase : IDisposable
	{
		internal const Int32 AccessViolation = unchecked((Int32)0xC0000005);

		internal readonly TempDirectory _directory = new TempDirectory();

		internal OutOfProcessNativeParser? _parser;

		internal Int32 _textCounter;

		public void Dispose()
		{
			this._parser?.Dispose();
			this._directory.Dispose();
		}

		internal static NativeParseResult SampleResult()
		{
			var argument = new ScriptVariable("who", VariableScope.Local)
			{
				DeclaredFile = @"C:\s\a.ssl",
				DeclaredLine = 5,
				ArgumentDeclaredLine = 9,
				InitialValue = "\"x\"",
				InitialValueKind = ValueKind.String,
				References = new[] { new SourceReference(@"C:\s\a.ssl", 12) },
			};
			var procedure = new ScriptProcedure("start")
			{
				Flags = ProcedureFlags.Export | ProcedureFlags.Critical,
				DeclaredFile = @"C:\s\a.ssl",
				StartFile = @"C:\s\b.h",
				FileName = "a.ssl",
				DeclaredLine = 3,
				DefinedLine = 9,
				StartLine = 9,
				EndLine = 20,
				ArgumentCount = 1,
				TimeValue = 4,
				Variables = new[] { argument },
				References = new[] { new SourceReference(@"C:\s\a.ssl", 30), new SourceReference(@"C:\s\b.h", 2) },
			};
			var global = new ScriptVariable("g_count", VariableScope.Global)
			{
				DeclaredFile = @"C:\s\a.ssl",
				FileName = "a.ssl",
				DeclaredLine = 2,
				InitialValue = "1.5",
				InitialValueKind = ValueKind.Float,
				ArrayLength = 0,
			};
			return new NativeParseResult(NativeParseStatus.Completed)
			{
				ExitCode = -1,
				Procedures = new[] { procedure },
				Variables = new[] { global },
				Log = "[Error] <Semantic> <C:\\s\\a.ssl>:1:2: text \"quoted\" \u00e9\r\n",
			};
		}

		internal static FakeParserHost Normal(Int32 index)
		{
			return new FakeParserHost(AnswerNormally);
		}

		internal static void AnswerNormally(FakeParserHost host, String line)
		{
			HostRequest request = HostProtocol.DeserializeRequest(line);
			if (request.Command == HostProtocol.ParseCommand)
			{
				host.Respond(request, SampleResult());
			}
			else
			{
				host.Respond(request, new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0 });
			}
		}

		/// <summary>A host that answers pings and calls <paramref name="onParse"/> for parse requests.</summary>
		internal static FakeParserHost Scripted(Action<FakeParserHost, HostRequest> onParse)
		{
			return new FakeParserHost((host, line) =>
			{
				HostRequest request = HostProtocol.DeserializeRequest(line);
				if (request.Command == HostProtocol.ParseCommand)
				{
					onParse(host, request);
				}
				else
				{
					host.Respond(request, new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0 });
				}
			});
		}

		internal FakeParserHostLauncher Create(Func<Int32, FakeParserHost> factory, NativeParserHostOptions? options = null, TimeProvider? time = null)
		{
			var launcher = new FakeParserHostLauncher(factory);
			String hostPath = this._directory.Write("host\\FalloutScriptStudio.ParserHost.exe", "x");
			this._parser = new OutOfProcessNativeParser(
				(options ?? new NativeParserHostOptions()) with { HostPath = hostPath, WorkingDirectoryRoot = this._directory.Path },
				null,
				launcher,
				time);
			return launcher;
		}

		internal ParserOptions Options()
		{
			String library = Path.Combine(this._directory.Path, "resources", "parser.dll");
			if (!File.Exists(library))
			{
				this._directory.Write(Path.Combine("resources", "parser.dll"), "x");
			}

			return new ParserOptions
			{
				NativeParserPath = library,
				PreprocessorDefinitions = "FOO=1",
				HeadersDirectory = @"C:\headers",
				BackwardCompatibilityMode = 1,
				ScratchDirectory = @"C:\scr",
				ScratchFileName = "p.ssl",
			};
		}

		internal Task<NativeParseResult> ParseAsync(CancellationToken cancellationToken = default, String? text = null)
		{
			// Every call parses another text unless the test says otherwise: a text that crashed the host is not sent again.
			text ??= "procedure start" + (++this._textCounter).ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n";
			return this._parser!.ParseAsync(text, Path.Combine(this._directory.Path, "scripts", "a.ssl"), this.Options(), cancellationToken);
		}
	}
}
