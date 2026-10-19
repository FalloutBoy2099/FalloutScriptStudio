namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.TestSupport;

	/// <summary>A uniquely named scratch folder in <c>_test_files\work</c> that is deleted when disposed.</summary>
	internal sealed class ScriptToolsTempDirectory : TestFolder
	{
		public ScriptToolsTempDirectory()
			: base("scripttools")
		{
		}

		public override String Combine(params String[] parts)
		{
			return System.IO.Path.GetFullPath(System.IO.Path.Combine(this.Path, System.IO.Path.Combine(parts)));
		}

		public String Write(String relativePath, String content, Encoding? encoding = null)
		{
			String full = this.Combine(relativePath);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
			File.WriteAllBytes(full, (encoding ?? new UTF8Encoding(false)).GetBytes(content));
			return full;
		}
	}
}