namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>
	/// Converts the data held by a successfully completed native parse into the public domain model.
	/// The interop structures never leave this class.
	/// </summary>
	internal static class NativeDataReader
	{
		/// <summary>Reads every variable and procedure from the native parser.</summary>
		/// <param name="library">The library that has just parsed the script.</param>
		/// <param name="scriptLines">The lines of the parsed script, used to find procedure argument declarations.</param>
		/// <param name="procedures">The procedures.</param>
		/// <param name="variables">The script-level variables.</param>
		internal static void Read(NativeParserLibrary library, String[] scriptLines, out List<ScriptProcedure> procedures, out List<ScriptVariable> variables)
		{
			Byte[] names = new Byte[library.NamespaceSize()];
			Int32 stringsSize = library.StringspaceSize();
			library.GetNamespace(names);
			Byte[]? strings = null;
			if (stringsSize > 0)
			{
				strings = new Byte[stringsSize];
				library.GetStringspace(strings);
			}

			Int32 variableCount = library.NumVars();
			variables = new List<ScriptVariable>(variableCount);
			for (Int32 i = 0; i < variableCount; i++)
			{
				library.GetVar(i, out VariableData data);
				IReadOnlyList<SourceReference> references = ReadReferences(data.NumRefs, buffer => library.GetVarRefs(i, buffer));
				variables.Add(CreateVariable(data, ParseName(names, data.Name), strings, references, -1, true));
			}

			Int32 procedureCount = library.NumProcs();
			procedures = new List<ScriptProcedure>(procedureCount);
			for (Int32 i = 0; i < procedureCount; i++)
			{
				procedures.Add(ReadProcedure(library, i, names, strings, scriptLines));
			}
		}

		/// <summary>Reads a name from a name table: a 16-bit length stored in the 6 bytes before the name, then the characters.</summary>
		internal static String ParseName(Byte[] table, Int32 offset)
		{
			if (offset < 6 || offset - 4 > table.Length)
			{
				throw new InvalidDataException("The native parser returned an invalid name offset (" + offset + ").");
			}

			Int32 length = (table[offset - 5] << 8) + table[offset - 6];
			if (offset - 4 + length > table.Length)
			{
				throw new InvalidDataException("The native parser returned an invalid name length (" + length + ").");
			}

			return Encoding.ASCII.GetString(table, offset - 4, length).TrimEnd('\0');
		}

		/// <summary>
		/// Finds the line on which a local variable is declared as an argument in the procedure header (legacy
		/// <c>ParseProcedureArguments</c>).
		/// </summary>
		/// <param name="start">1-based line where the procedure body starts.</param>
		/// <param name="end">Line (as reported by the native parser) at which to stop searching upwards.</param>
		/// <param name="lowerName">Lower-cased variable name.</param>
		/// <param name="code">The lines of the script.</param>
		/// <returns>The 1-based line, or -1 when the variable is not an argument.</returns>
		internal static Int32 FindArgumentDeclarationLine(Int32 start, Int32 end, String lowerName, String[] code)
		{
			if (start > code.Length)
			{
				return -1;
			}

			Int32 length = SslKeywords.Variable.Length + lowerName.Length;
			for (Int32 i = start - 1; i > end; i--)
			{
				String line = code[i].TrimStart().ToLowerInvariant();
				if (length > line.Length)
				{
					continue;
				}

				Int32 keyword = line.IndexOf(SslKeywords.Variable, StringComparison.Ordinal);
				if (keyword == -1)
				{
					continue;
				}

				line = SslSyntax.RemoveDoubleWhiteSpaces(line, keyword, 0);
				while (true)
				{
					Int32 x = line.IndexOf(SslKeywords.Variable + lowerName, keyword, StringComparison.Ordinal);
					if (x == -1)
					{
						break;
					}

					if (x + length < line.Length && SslSyntax.IsLetterDigitOrUnderscore(line[x + length]))
					{
						line = line.Remove(x, length);
						continue;
					}

					Int32 begin = line.IndexOf(SslKeywords.Begin, StringComparison.Ordinal);
					if (begin > -1 && x > begin)
					{
						return -1;
					}

					return i + 1;
				}

				if (line.StartsWith(SslKeywords.Procedure, StringComparison.Ordinal))
				{
					break;
				}
			}

			return -1;
		}

		private static ScriptProcedure ReadProcedure(NativeParserLibrary library, Int32 index, Byte[] names, Byte[]? strings, String[] scriptLines)
		{
			library.GetProc(index, out ProcedureData data);

			String declaredFile = ReadFullPath(data.DeclaredFile) ?? String.Empty;
			String startFile = ReadFullPath(data.StartFile) ?? String.Empty;
			IReadOnlyList<SourceReference> references = ReadReferences(data.NumRefs, buffer => library.GetProcRefs(index, buffer));

			var locals = new List<ScriptVariable>();
			Int32 namespaceSize = library.GetProcNamespaceSize(index);
			if (namespaceSize != -1)
			{
				Byte[] localNames = new Byte[namespaceSize];
				library.GetProcNamespace(index, localNames);
				for (Int32 j = 0; j < data.NumVariables; j++)
				{
					library.GetProcVar(index, j, out VariableData local);
					String name = ParseName(localNames, local.Name);
					IReadOnlyList<SourceReference> localReferences = ReadReferences(local.NumRefs, buffer => library.GetProcVarRefs(index, j, buffer));
					Int32 argumentLine = FindArgumentDeclarationLine(data.Start, local.Declared, name.ToLowerInvariant(), scriptLines);
					locals.Add(CreateVariable(local, name, strings, localReferences, argumentLine, false));
				}
			}

			return new ScriptProcedure(ParseName(names, data.Name))
			{
				Flags = data.Type,
				DeclaredFile = declaredFile,
				StartFile = startFile,
				FileName = String.IsNullOrEmpty(declaredFile) ? String.Empty : Path.GetFileName(declaredFile).ToLowerInvariant(),
				DeclaredLine = data.Declared,
				DefinedLine = data.Defined == -1 ? data.Start : data.Defined,
				StartLine = data.Start,
				EndLine = data.End,
				ArgumentCount = data.Args,
				TimeValue = data.Time,
				Variables = locals,
				References = references,
			};
		}

		private static ScriptVariable CreateVariable(VariableData data, String name, Byte[]? strings, IReadOnlyList<SourceReference> references, Int32 argumentLine, Boolean normalizeFile)
		{
			String? initialValue = null;
			if (data.Initialized != 0)
			{
				initialValue = data.InitialType switch
				{
					ValueKind.Int => data.IntValue.ToString(CultureInfo.InvariantCulture),
					ValueKind.Float => data.FloatValue.ToString(CultureInfo.InvariantCulture),
					ValueKind.String when strings is not null => '"' + ParseName(strings, data.IntValue) + '"',
					_ => null,
				};
			}

			String? declaredFile = normalizeFile ? ReadFullPath(data.DeclaredFile) : Marshal.PtrToStringAnsi(data.DeclaredFile);
			return new ScriptVariable(name, data.Type)
			{
				DeclaredFile = declaredFile,
				FileName = normalizeFile && declaredFile is not null ? Path.GetFileName(declaredFile).ToLowerInvariant() : null,
				DeclaredLine = data.Declared,
				ArgumentDeclaredLine = argumentLine,
				InitialValue = initialValue,
				InitialValueKind = data.Initialized != 0 ? data.InitialType : ValueKind.None,
				ArrayLength = Math.Max(0, data.ArrayLength),
				References = references,
			};
		}

		private static IReadOnlyList<SourceReference> ReadReferences(Int32 count, Action<Int32[]> fill)
		{
			if (count <= 0)
			{
				return Array.Empty<SourceReference>();
			}

			Int32[] buffer = new Int32[count * 2];
			fill(buffer);

			var references = new SourceReference[count];
			for (Int32 i = 0; i < count; i++)
			{
				// Each pair is (line, pointer to the ANSI file name); the native parser is a 32-bit library.
				references[i] = SourceReference.Create(Marshal.PtrToStringAnsi(new IntPtr(buffer[i * 2 + 1])), buffer[i * 2]);
			}

			return references;
		}

		private static String? ReadFullPath(IntPtr pointer)
		{
			if (pointer == IntPtr.Zero)
			{
				return null;
			}

			String? path = Marshal.PtrToStringAnsi(pointer);
			return String.IsNullOrEmpty(path) ? null : Path.GetFullPath(path);
		}
	}
}
