namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Security.Cryptography;
	using System.Text;
	using Compilation;

	/// <summary>
	/// A digest of everything besides the sources that decides what the compiler produces: the options that reach the command line, and the
	/// tools themselves. A compiled script is up to date only when both digests are the ones it was built with.
	/// </summary>
	/// <param name="Options">The SHA-256 (hexadecimal) of the effective option set.</param>
	/// <param name="Tools">The SHA-256 (hexadecimal) of the size and time stamp of <c>compile.exe</c> and of the preprocessor in use.</param>
	public sealed record BuildFingerprint(String Options, String Tools)
	{
		/// <summary>The version of the digest layout; a new layout invalidates every cache.</summary>
		internal const Int32 Layout = 1;

		/// <summary>Computes the fingerprint of a set of compiler options.</summary>
		/// <param name="options">The effective compiler options.</param>
		/// <returns>The fingerprint.</returns>
		public static BuildFingerprint Create(CompilerOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);

			StringBuilder text = new StringBuilder();
			text.Append("layout=").Append(Layout.ToString(CultureInfo.InvariantCulture));
			text.Append(";optimization=").Append(((Int32)options.Optimization).ToString(CultureInfo.InvariantCulture));
			text.Append(";shortCircuit=").Append(options.ShortCircuit ? '1' : '0');
			text.Append(";backward=").Append(options.BackwardCompatibility ? '1' : '0');
			text.Append(";debug=").Append(options.DebugInfo ? '1' : '0');
			text.Append(";preprocessor=").Append(((Int32)options.Preprocessor).ToString(CultureInfo.InvariantCulture));
			text.Append(";define=").Append(String.IsNullOrWhiteSpace(options.PreprocessorDefine) ? String.Empty : options.PreprocessorDefine.Trim());
			text.Append(";includes=").Append(options.SearchesIncludes ? '1' : '0');
			text.Append(";headers=").Append(options.SearchesIncludes ? Normalize(options.HeadersDirectory) : String.Empty);

			return new BuildFingerprint(Hash(text.ToString()), Hash(DescribeTools(options)));
		}

		private static String DescribeTools(CompilerOptions options)
		{
			List<String> names = ["compile.exe"];
			switch (options.Preprocessor)
			{
				case PreprocessorKind.Mcpp:
					names.Add("mcpp.bat");
					names.Add("mcpp.exe");
					break;
				case PreprocessorKind.Watcom:
					names.Add("wcc.bat");
					names.Add("wcc386.exe");
					break;
			}

			StringBuilder text = new StringBuilder();
			foreach (String name in names)
			{
				FileStamp stamp = FileStamp.Read(Path.Combine(options.ResourcesDirectory, name));
				text.Append(name).Append('=').Append(stamp.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(stamp.UtcTicks.ToString(CultureInfo.InvariantCulture)).Append(';');
			}

			return text.ToString();
		}

		private static String Normalize(String? path)
		{
			if (String.IsNullOrWhiteSpace(path))
			{
				return String.Empty;
			}

			try
			{
				return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToUpperInvariant();
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path.Trim().ToUpperInvariant();
			}
		}

		private static String Hash(String text)
		{
			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
		}
	}
}