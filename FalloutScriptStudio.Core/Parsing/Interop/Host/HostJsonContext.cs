namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System.Text.Json.Serialization;

	/// <summary>Source-generated JSON metadata of the host protocol (no reflection, trimming friendly).</summary>
	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
	[JsonSerializable(typeof(HostRequest))]
	[JsonSerializable(typeof(HostResponse))]
	internal sealed partial class HostJsonContext : JsonSerializerContext
	{
	}
}