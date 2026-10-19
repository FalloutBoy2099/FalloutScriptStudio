namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The state of a script and of its dependencies at the moment its compilation starts. It is created before the compiler runs, so that a
	/// file edited during the compilation does not make the result look newer than it is. After the compilation the caller reports the
	/// outcome: <see cref="Succeeded"/> records the build, <see cref="Failed"/> forgets it. A cancelled compilation reports nothing.
	/// </summary>
	public sealed class BuildTicket
	{
		private readonly BuildCache _cache;
		private readonly BuildFingerprint _fingerprint;
		private readonly Boolean _shortCircuit;
		private readonly FileStamp _source;
		private readonly IReadOnlyList<DependencyStamp> _dependencies;

		internal BuildTicket(
			BuildCache cache,
			String script,
			String? output,
			BuildFingerprint fingerprint,
			Boolean shortCircuit,
			FileStamp source,
			IReadOnlyList<DependencyStamp>? dependencies)
		{
			this._cache = cache;
			this.ScriptPath = script;
			this.OutputPath = output;
			this._fingerprint = fingerprint;
			this._shortCircuit = shortCircuit;
			this._source = source;
			this._dependencies = dependencies ?? Array.Empty<DependencyStamp>();
			this.IsCacheable = output is not null && dependencies is not null && source.Exists;
		}

		/// <summary>Gets the script.</summary>
		public String ScriptPath { get; }

		/// <summary>Gets the compiled file the script is built into, or <see langword="null"/> when it is not known.</summary>
		public String? OutputPath { get; }

		/// <summary>Gets a value indicating whether a successful build can be remembered (the output file and every dependency are known).</summary>
		public Boolean IsCacheable { get; }

		/// <summary>Remembers the build as up to date: call it when the compiler succeeded.</summary>
		public void Succeeded()
		{
			if (!this.IsCacheable || this.OutputPath is null)
			{
				this.Failed();
				return;
			}

			FileStamp output = FileStamp.Read(this.OutputPath);
			if (!output.Exists)
			{
				this.Failed();
				return;
			}

			this._cache.Set(new BuildRecord(this.ScriptPath, this.OutputPath, this._fingerprint, this._shortCircuit, this._source, output, this._dependencies));
		}

		/// <summary>Forgets the previous build of the script: call it when the compiler failed. A failed compilation is never up to date.</summary>
		public void Failed()
		{
			if (this.OutputPath is not null)
			{
				this._cache.Remove(this.ScriptPath, this.OutputPath);
			}
		}
	}
}