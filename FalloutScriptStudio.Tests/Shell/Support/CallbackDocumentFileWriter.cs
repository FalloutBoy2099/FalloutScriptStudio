namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Workspace;

	/// <summary>A document file writer that runs a test action instead of (or around) the real write.</summary>
	internal sealed class CallbackDocumentFileWriter : IDocumentFileWriter
	{
		private readonly Func<String, Byte[], Task> _write;

		public CallbackDocumentFileWriter(Func<String, Byte[], Task> write)
		{
			ArgumentNullException.ThrowIfNull(write);
			this._write = write;
		}

		public Task WriteAsync(String path, Byte[] content)
		{
			return this._write(path, content);
		}
	}
}