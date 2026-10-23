namespace FalloutScriptStudio.Core.IO
{
	using System;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>
	/// Text encodings used by Fallout scripts and message files.
	/// </summary>
	/// <remarks>
	/// <see cref="Encoding.Default"/> is UTF-8 on .NET Core, whereas Fallout files are written
	/// with the Windows ANSI code page, so the legacy code pages must be requested explicitly.
	/// </remarks>
	public static class TextEncodings
	{
		private const Int32 FallbackCodePage = 1252;

		private static readonly Lazy<Encoding> _ansi = new Lazy<Encoding>(() => ResolveSystemEncoding(GetSystemAnsiCodePage));
		private static readonly Lazy<Encoding> _oem = new Lazy<Encoding>(() => ResolveSystemEncoding(GetSystemOemCodePage));
		private static readonly Lazy<Encoding> _oem866 = new Lazy<Encoding>(() => GetEncoding(866));

		static TextEncodings()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		/// <summary>
		/// Gets the Windows ANSI code page of the system (<c>GetACP</c>, what .NET Framework called <c>Encoding.Default</c>); it does
		/// not depend on the regional format of the user. Windows-1252 when the system code page cannot be determined or is a Unicode one.
		/// </summary>
		public static Encoding Ansi => _ansi.Value;

		/// <summary>
		/// Gets the OEM code page of the system (<c>GetOEMCP</c>), the one the console and the batch files print in.
		/// </summary>
		public static Encoding Oem => _oem.Value;

		/// <summary>Gets the DOS Cyrillic code page (cp866).</summary>
		public static Encoding Oem866 => _oem866.Value;

		/// <summary>Gets UTF-8 without a byte order mark.</summary>
		public static Encoding Utf8 { get; } = new UTF8Encoding(false);

		/// <summary>Gets the encoding of a code page (the legacy code pages are registered first).</summary>
		/// <param name="codePage">The code page number (1252, 1251, 866...).</param>
		/// <returns>The encoding; Windows-1252 when the code page is not available on this system.</returns>
		public static Encoding FromCodePage(Int32 codePage)
		{
			return GetEncoding(codePage);
		}

		/// <summary>
		/// Gets the encoding of a system code page reported by <paramref name="getCodePage"/>. A Unicode code page (a system with the
		/// UTF-8 option for legacy programs), an unknown or unsupported one, or a failing probe gives Windows-1252.
		/// </summary>
		/// <param name="getCodePage">Returns the code page of the system.</param>
		/// <returns>The encoding.</returns>
		public static Encoding ResolveSystemEncoding(Func<Int32> getCodePage)
		{
			ArgumentNullException.ThrowIfNull(getCodePage);

			Int32 codePage;
			try
			{
				codePage = getCodePage();
			}
			catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
			{
				codePage = FallbackCodePage;
			}

			Boolean unicode = codePage is 65000 or 65001 or 1200 or 1201 or 12000 or 12001;
			return GetEncoding(codePage <= 0 || unicode ? FallbackCodePage : codePage);
		}

		private static Int32 GetSystemAnsiCodePage()
		{
			return OperatingSystem.IsWindows() ? (Int32)GetACP() : FallbackCodePage;
		}

		private static Int32 GetSystemOemCodePage()
		{
			return OperatingSystem.IsWindows() ? (Int32)GetOEMCP() : FallbackCodePage;
		}

		[DllImport("kernel32.dll")]
		private static extern UInt32 GetACP();

		[DllImport("kernel32.dll")]
		private static extern UInt32 GetOEMCP();

		private static Encoding GetEncoding(Int32 codePage)
		{
			try
			{
				return Encoding.GetEncoding(codePage);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
			{
				return codePage == FallbackCodePage ? Encoding.Latin1 : GetEncoding(FallbackCodePage);
			}
		}
	}
}