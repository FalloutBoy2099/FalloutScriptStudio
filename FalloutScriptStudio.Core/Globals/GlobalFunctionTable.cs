namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The functions and macros that read or write a global variable, with the names they have in <c>opcodes.txt</c> and
	/// <c>functions.ini</c>. The first argument of every one of them is the variable. <c>set_sfall_arg</c> and <c>get_sfall_arg</c> are not
	/// in this table on purpose: they exchange the arguments of a hook with sfall, they do not touch a global variable.
	/// </summary>
	public static class GlobalFunctionTable
	{
		private static readonly GlobalFunction[] _functions = Build();
		private static readonly Dictionary<String, GlobalFunction> _byName = CreateLookup(_functions);

		/// <summary>Gets the functions of the table.</summary>
		public static IReadOnlyList<GlobalFunction> Functions => _functions;

		/// <summary>Finds a function by name.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <param name="function">Receives the function.</param>
		/// <returns><see langword="true"/> when the function reads or writes a global variable.</returns>
		public static Boolean TryGet(ReadOnlySpan<Char> name, out GlobalFunction? function)
		{
			Dictionary<String, GlobalFunction>.AlternateLookup<ReadOnlySpan<Char>> lookup = _byName.GetAlternateLookup<ReadOnlySpan<Char>>();
			return lookup.TryGetValue(name, out function);
		}

		private static Dictionary<String, GlobalFunction> CreateLookup(GlobalFunction[] functions)
		{
			Dictionary<String, GlobalFunction> lookup = new(functions.Length, StringComparer.OrdinalIgnoreCase);
			foreach (GlobalFunction function in functions)
			{
				lookup.Add(function.Name, function);
			}

			return lookup;
		}

		private static GlobalFunction[] Build()
		{
			List<GlobalFunction> list = [];
			(String Prefix, GlobalVariableKind Kind, String Bit)[] families =
			[
				("global", GlobalVariableKind.GameVariable, "gvar"),
				("map", GlobalVariableKind.MapVariable, "mvar"),
				("local", GlobalVariableKind.LocalVariable, "lvar"),
			];
			foreach ((String prefix, GlobalVariableKind kind, String bit) in families)
			{
				list.Add(new GlobalFunction(prefix + "_var", kind, GlobalAccess.Read, -1, GlobalValueType.Unknown));
				list.Add(new GlobalFunction("set_" + prefix + "_var", kind, GlobalAccess.Write, 1, GlobalValueType.Unknown));
				foreach (String step in new[] { "inc_", "dec_" })
				{
					list.Add(new GlobalFunction(step + prefix + "_var", kind, GlobalAccess.ReadWrite, -1, GlobalValueType.Unknown));
					list.Add(new GlobalFunction(step + prefix + "_var_amt", kind, GlobalAccess.ReadWrite, -1, GlobalValueType.Unknown));
				}

				list.Add(new GlobalFunction(bit + "_bit", kind, GlobalAccess.Read, -1, GlobalValueType.Unknown));
				list.Add(new GlobalFunction("set_" + bit + "_bit_on", kind, GlobalAccess.ReadWrite, -1, GlobalValueType.Unknown));
				list.Add(new GlobalFunction("set_" + bit + "_bit_off", kind, GlobalAccess.ReadWrite, -1, GlobalValueType.Unknown));
			}

			list.Add(new GlobalFunction("set_sfall_global", GlobalVariableKind.SfallGlobal, GlobalAccess.Write, 1, GlobalValueType.Unknown));
			list.Add(new GlobalFunction("get_sfall_global_int", GlobalVariableKind.SfallGlobal, GlobalAccess.Read, -1, GlobalValueType.Int));
			list.Add(new GlobalFunction("get_sfall_global_float", GlobalVariableKind.SfallGlobal, GlobalAccess.Read, -1, GlobalValueType.Float));
			return list.ToArray();
		}
	}
}