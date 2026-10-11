namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// One decompilation of one script. The work goes in stages, one source file each: <c>Reading</c> reads the compiled file (its
	/// tables, the startup code, the code of each procedure), <c>Definitions</c> works out what the names of the namespace are,
	/// <c>Reduction</c>, <c>Tree</c> and <c>Blocks</c> turn the stack code into trees of expressions and statements, and
	/// <c>Source</c>, <c>Expressions</c> and <c>Naming</c> write them as source text.
	/// </summary>
	internal sealed partial class DecompilationSession
	{
		private readonly BinaryScriptReader _reader;

		private readonly ScriptDecompilerOptions _options;

		private readonly List<String> _messages = [];

		private readonly ProcedureTable _procedures = new();

		private readonly ScriptNamespace _namespace = new();

		private readonly ScriptNamespace _strings = new();

		private readonly List<ScriptOpcode> _globalVariables = [];

		private readonly List<ScriptOpcode> _exportedVariables = [];

		private readonly List<ScriptOpcode> _exportedVariableValues = [];

		private readonly List<ScriptOpcode> _exportedProcedures = [];

		private readonly List<List<Node>> _procedureBodies = [];

		private readonly List<List<Node>> _conditions = [];

		private readonly Dictionary<UInt32, DefinitionObject> _definitions = [];

		private readonly List<String> _globalVariableNames = [];

		public DecompilationSession(Byte[] data, ScriptDecompilerOptions options)
		{
			this._reader = new BinaryScriptReader(data);
			this._options = options;
		}

		public IReadOnlyList<String> Messages => this._messages;
	}
}
