namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Text;
	using System.Text.Json;

	/// <summary>
	/// The wire format shared by the application and <c>FalloutScriptStudio.ParserHost.exe</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The host reads one JSON request per line from its standard input and writes one response per request to its
	/// standard output. A response line contains <see cref="ResponsePrefix"/> followed by the JSON text, so everything else the
	/// native library may print to the shared standard output (the C runtime inside <c>parser.dll</c> writes to it)
	/// cannot be mistaken for a response: the reader skips lines without the prefix. Both sides use UTF-8 without BOM.
	/// </para>
	/// <para>The host exits when its standard input is closed or when it receives the <see cref="ExitCommand"/>.</para>
	/// </remarks>
	internal static class HostProtocol
	{
		/// <summary>The protocol version; a request or response with another version is rejected.</summary>
		public const Int32 Version = 1;

		/// <summary>The command that parses a script.</summary>
		public const String ParseCommand = "parse";

		/// <summary>The command that makes the host answer immediately without loading the native library.</summary>
		public const String PingCommand = "ping";

		/// <summary>The command that makes the host exit.</summary>
		public const String ExitCommand = "exit";

		/// <summary>The marker that starts the JSON of every response line.</summary>
		public const String ResponsePrefix = "@@FSS-PARSER@@";

		/// <summary>Gets the encoding of the pipes.</summary>
		public static Encoding Encoding { get; } = new UTF8Encoding(false);

		/// <summary>Serializes a request to one line (without the line break).</summary>
		public static String SerializeRequest(HostRequest request)
		{
			return JsonSerializer.Serialize(request, HostJsonContext.Default.HostRequest);
		}

		/// <summary>Serializes a response to one line, including <see cref="ResponsePrefix"/> and without the line break.</summary>
		public static String SerializeResponse(HostResponse response)
		{
			return ResponsePrefix + JsonSerializer.Serialize(response, HostJsonContext.Default.HostResponse);
		}

		/// <summary>Deserializes a request line.</summary>
		/// <exception cref="JsonException">The line is not a valid request.</exception>
		public static HostRequest DeserializeRequest(String line)
		{
			return JsonSerializer.Deserialize(line, HostJsonContext.Default.HostRequest)
				?? throw new JsonException("The request is empty.");
		}

		/// <summary>
		/// Tries to read a response line.
		/// </summary>
		/// <param name="line">A line read from the host.</param>
		/// <param name="response">The response when the line is one.</param>
		/// <returns><c>false</c> when the line does not contain <see cref="ResponsePrefix"/> (it is noise from the native library).</returns>
		/// <exception cref="JsonException">The line contains the prefix but its JSON is invalid.</exception>
		public static Boolean TryReadResponse(String line, out HostResponse? response)
		{
			response = null;
			Int32 start = line.IndexOf(ResponsePrefix, StringComparison.Ordinal);
			if (start < 0)
			{
				return false;
			}

			response = JsonSerializer.Deserialize(line.AsSpan(start + ResponsePrefix.Length), HostJsonContext.Default.HostResponse)
				?? throw new JsonException("The response is empty.");
			return true;
		}
	}
}
