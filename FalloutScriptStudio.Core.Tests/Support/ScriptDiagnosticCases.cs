namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The table of <see cref="ScriptDiagnosticCase"/>s: every rule of the analysis with scripts that sslc rejects and scripts that it accepts.</summary>
	internal static class ScriptDiagnosticCases
	{
		public static IReadOnlyList<ScriptDiagnosticCase> Create()
		{
			List<ScriptDiagnosticCase> cases = [];
			AddArguments(cases);
			AddNames(cases);
			AddDeclarations(cases);
			AddStructure(cases);
			AddStatements(cases);
			AddFunctions(cases);
			AddConditional(cases);
			AddMacros(cases);
			AddAccepted(cases);

			HashSet<String> names = new(StringComparer.Ordinal);
			foreach (ScriptDiagnosticCase item in cases)
			{
				if (!names.Add(item.Name))
				{
					throw new InvalidOperationException("Two cases are called " + item.Name);
				}
			}

			return cases;
		}

		private static void AddArguments(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: too few", "procedure foo(variable a, variable b) begin return a + b; end", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: too many", "procedure foo(variable a, variable b) begin return a + b; end", "procedure start begin", "\tvariable x;", "\tx := foo(1, 2, 3);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: a default value allows fewer", "procedure foo(variable a, variable b := 5) begin return a + b; end", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "\tx := foo(1, 2);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: none given for a procedure with one required", "procedure foo(variable a, variable b := 5) begin return a + b; end", "procedure start begin", "\tvariable x;", "\tx := foo();", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: a procedure without arguments used bare", "procedure foo begin return 1; end", "procedure start begin", "\tvariable x;", "\tx := foo;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: a procedure without arguments with empty parentheses", "procedure foo begin return 1; end", "procedure start begin", "\tvariable x;", "\tx := foo();", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: one given to a procedure without arguments", "procedure foo begin return 1; end", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: the forward declaration fixes the count", "procedure foo(variable a, variable b);", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end", "procedure foo(variable a, variable b) begin return 1; end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: a forward declaration with default values", "procedure foo(variable a, variable b := 2);", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end", "procedure foo(variable a, variable b) begin return 1; end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: call statement with too many", "procedure foo(variable a) begin end", "procedure start begin", "\tcall foo(1, 2);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: call statement without parentheses needs none", "procedure foo(variable a) begin end", "procedure start begin", "\tcall foo;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: call statement with empty parentheses needs none", "procedure foo(variable a) begin end", "procedure start begin", "\tcall foo();", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: call statement without parentheses and default values", "procedure foo(variable a := 1) begin end", "procedure start begin", "\tcall foo;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: a nested call is checked too", "procedure foo(variable a, variable b) begin return 1; end", "procedure start begin", "\tvariable x;", "\tx := foo(foo(1, 2, 3), 3);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: commas inside a call, a string and an index do not count", "procedure foo(variable a, variable b) begin return 1; end", "procedure start begin", "\tvariable x, arr;", "\tarr := create_array(3, 0);", "\tx := foo(random(1, 2), 3);", "\tx := foo(\"a,b\", 3);", "\tx := foo(arr[1], 3);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1001", "arguments: an imported procedure", "import procedure foo(variable a);", "procedure start begin", "\tcall foo(1, 2);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: an imported procedure is called", "import procedure foo(variable a);", "procedure start begin", "\tcall foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: a macro that expands to two arguments", "#define PAIR 1, 2", "procedure foo(variable a, variable b) begin return 1; end", "procedure start begin", "\tvariable x;", "\tx := foo(PAIR);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: a procedure name is passed bare", "procedure foo(variable a, variable b) begin end", "procedure start begin", "\tregister_hook_proc(2, foo);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("arguments: recursion", "procedure foo(variable a) begin", "\tif (a > 0) then call foo(a - 1);", "end", "procedure start begin", "\tcall foo(3);", "end"));
		}

		private static void AddNames(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1002", "names: a call of an unknown name", "procedure start begin", "\tundefined_thing(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1003", "names: an unknown name in an expression", "procedure start begin", "\tvariable x;", "\tx := undefined_thing;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1003", "names: an unknown name as a statement", "procedure start begin", "\tundefined_thing;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1003", "names: an unknown name in a condition", "procedure start begin", "\tif (undefined_thing) then begin end", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1003", "names: an unknown case label", "procedure start begin", "\tvariable a;", "\ta := 1;", "\tswitch (a) begin", "\t\tcase UNDEFINED_C: display_msg(\"a\");", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1003", "names: the variable of foreach must be declared", "procedure start begin", "\tvariable arr;", "\tarr := create_array(3, 0);", "\tforeach (i in arr) begin", "\t\tdisplay_msg(\"\" + i);", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: foreach declares its variables with the keyword", "procedure start begin", "\tvariable arr;", "\tarr := create_array(3, 0);", "\tforeach (variable k: v in arr) begin", "\t\tdisplay_msg(\"\" + k + v);", "\tend", "\tforeach (variable k: v in arr) begin", "\t\tdisplay_msg(\"\" + k + v);", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1002", "names: a typo of an engine function", "procedure start begin", "\tdisplay_msgg(\"a\");", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1003", "names: an unknown name in the argument of a macro", "#define SHOW(x) display_msg(x)", "procedure start begin", "\tSHOW(undefined_thing);", "end"));

			cases.Add(ScriptDiagnosticCase.Rejected("SSL1004", "names: a procedure called before its definition", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end", "procedure foo(variable a) begin return a; end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1004", "names: a call statement before the definition", "procedure start begin", "\tcall foo;", "end", "procedure foo begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1004", "names: a delayed call before the definition", "procedure start begin", "\tcall foo in 5;", "end", "procedure foo begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a forward declaration allows the early call", "procedure foo(variable a);", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end", "procedure foo(variable a) begin return a; end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a forward declaration without arguments", "procedure foo;", "procedure start begin", "\tvariable x;", "\tx := foo;", "end", "procedure foo begin return 1; end"));

			cases.Add(ScriptDiagnosticCase.Rejected("SSL1005", "names: a local used before its declaration", "procedure start begin", "\ta := 1;", "\tvariable a;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1005", "names: a local read before its declaration", "procedure start begin", "\tvariable b;", "\tb := a;", "\tvariable a;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1005", "names: a global declared after the procedure that uses it", "procedure start begin", "\tg := 2;", "end", "variable g;"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1006", "names: the local of another procedure", "procedure foo begin", "\tvariable a;", "\ta := 1;", "end", "procedure start begin", "\ta := 2;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a global declared before the procedure", "variable g;", "procedure start begin", "\tg := 2;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a variable declared after other statements", "procedure start begin", "\tdisplay_msg(\"a\");", "\tvariable a;", "\ta := 1;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a variable of a block is visible after the block", "procedure start begin", "\tif (1) then begin", "\t\tvariable q;", "\t\tq := 1;", "\tend", "\tq := 2;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a variable declared in a loop", "procedure start begin", "\tvariable i;", "\twhile (i < 2) do begin", "\t\ti++;", "\t\tvariable k;", "\t\tk := i;", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a local with the name of a procedure", "procedure foo begin end", "procedure start begin", "\tvariable foo;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: names after a dot are keys", "procedure start begin", "\tvariable arr;", "\tarr := create_array(3, 0);", "\tarr.undefined_key := 1;", "\tdisplay_msg(arr.other);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: the keywords and operators written as words", "procedure start begin", "\tvariable a;", "\ta := 5 bwand 3;", "\ta := not a;", "\tif (a and 1) then begin end", "\tif (a or 1) then begin end", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: names of a map literal", "procedure start begin", "\tvariable a;", "\ta := {1: 2, 3: 4};", "\ta := {\"a\": 2};", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a variable block at the top level", "variable begin", "\tg1 := 1;", "\tg2;", "\tg3 = 5;", "end", "procedure start begin", "\tg1 := g2 + g3;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: a variable block in a procedure", "procedure start begin", "\tvariable begin", "\t\ta := 1;", "\t\tb;", "\tend", "\ta := b;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1005", "names: a global variable block declared after the use", "procedure start begin", "\tg1 := 1;", "end", "variable begin", "\tg1;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("names: an exported and an imported variable", "export variable e;", "import variable m;", "procedure start begin", "\te := m;", "end"));
		}

		private static void AddDeclarations(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1008", "duplicates: a procedure defined twice", "procedure foo begin end", "procedure foo begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1008", "duplicates: the case of the name does not matter", "procedure Foo begin end", "procedure foo begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1008", "duplicates: a forward declaration after the definition", "procedure foo begin end", "procedure foo;", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1008", "duplicates: defined twice with a forward declaration", "procedure foo;", "procedure foo begin end", "procedure foo begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("duplicates: a forward declaration repeated", "procedure foo;", "procedure foo;", "procedure foo begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1009", "duplicates: a global variable declared twice", "variable a;", "variable a;", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1009", "duplicates: the case of a variable does not matter", "variable A;", "variable a;", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1009", "duplicates: a local declared twice", "procedure start begin", "\tvariable a;", "\tvariable a;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1009", "duplicates: the same name in two blocks of a procedure", "procedure start begin", "\tif (1) then begin", "\t\tvariable a;", "\t\ta := 1;", "\tend", "\tif (1) then begin", "\t\tvariable a;", "\t\ta := 2;", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1009", "duplicates: a local with the name of an argument", "procedure foo(variable a) begin", "\tvariable a;", "end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1009", "duplicates: an argument named twice", "procedure foo(variable a, variable a) begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("duplicates: a local hides a global", "variable a;", "procedure start begin", "\tvariable a;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("duplicates: the same local in two procedures", "procedure foo begin", "\tvariable a;", "end", "procedure start begin", "\tvariable a;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("duplicates: foreach variables of the same name", "procedure start begin", "\tvariable arr;", "\tarr := create_array(3, 0);", "\tforeach (variable p: d in arr) begin", "\t\tdisplay_msg(\"\" + p + d);", "\tend", "\tforeach (variable p: d in arr) begin", "\t\tdisplay_msg(\"\" + p + d);", "\tend", "end"));

			cases.Add(ScriptDiagnosticCase.Rejected("SSL1015", "declarations: a procedure declared and used but never defined", "procedure foo(variable a);", "procedure start begin", "\tcall foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("declarations: a procedure declared and not used", "procedure foo;", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1015", "declarations: a standard handler declared and not defined", "procedure start;", "procedure push_p_proc;", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("declarations: an exported forward declaration", "export procedure foo;", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1019", "declarations: the definition has more arguments than the declaration", "procedure foo(variable a);", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end", "procedure foo(variable a, variable b) begin return 1; end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1019", "declarations: the definition has fewer arguments than the declaration", "procedure foo(variable a);", "procedure foo begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1018", "declarations: default values in the definition of a declared procedure", "procedure foo(variable a, variable b);", "procedure foo(variable a, variable b := 2) begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1018", "declarations: default values in the declaration and the definition", "procedure foo(variable a, variable b := 2);", "procedure foo(variable a, variable b := 2) begin end", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("declarations: default values in the declaration only", "procedure foo(variable a, variable b := 2);", "procedure foo(variable a, variable b) begin end", "procedure start begin", "\tcall foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1017", "declarations: a default value before an argument without one", "procedure foo(variable a := 1, variable b) begin end", "procedure start begin", "\tcall foo(1, 2);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("declarations: the modifiers of a procedure", "inline procedure foo begin end", "pure procedure bar begin end", "critical procedure baz begin end", "procedure start begin end"));
		}

		private static void AddStructure(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1010", "structure: a begin without end at the end of the file", "procedure start begin", "\tdisplay_msg(\"a\");"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1010", "structure: a begin without end before the next procedure", "procedure start begin", "\tdisplay_msg(\"a\");", "procedure foo begin", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1010", "structure: an unclosed block inside a procedure", "procedure start begin", "\tif (1) then begin", "\t\tdisplay_msg(\"a\");", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1011", "structure: an end that closes nothing", "procedure start begin", "\tdisplay_msg(\"a\");", "end", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("structure: nested blocks are balanced", "procedure start begin", "\tvariable i;", "\twhile (i < 3) do begin", "\t\tif (i == 1) then begin", "\t\t\tdisplay_msg(\"a\");", "\t\tend else begin", "\t\t\tdisplay_msg(\"b\");", "\t\tend", "\t\ti++;", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("structure: keywords in strings and comments are not code", "procedure start begin", "\tdisplay_msg(\"begin end if then undefined_thing\");", "\t// begin if then undefined_thing(", "\t/* end end end undefined_thing */", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1012", "structure: if without then before begin", "procedure start begin", "\tif (1) begin", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1012", "structure: if without then before a statement", "procedure start begin", "\tif (1) display_msg(\"a\");", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1012", "structure: else if without then", "procedure start begin", "\tif (1) then begin", "\tend else if (2) begin", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1013", "structure: while without do", "procedure start begin", "\twhile (1) begin", "\t\tbreak;", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("structure: if and while without parentheses", "procedure start begin", "\tvariable a;", "\ta := 1;", "\tif a == 1 then begin", "\tend", "\twhile a < 3 do begin", "\t\ta++;", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("structure: if then else on statements", "procedure start begin", "\tif (1) then", "\t\tdisplay_msg(\"a\");", "\telse", "\t\tdisplay_msg(\"b\");", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("structure: for, foreach and switch", "procedure start begin", "\tvariable i, arr, a;", "\tarr := create_array(3, 0);", "\tfor (i := 0; i < 3; i++) begin", "\tend", "\tfor (i := 0; i < 3; i++) display_msg(\"a\");", "\tforeach (i in arr) begin", "\tend", "\ta := 1;", "\tswitch (a) begin", "\t\tcase 1: display_msg(\"a\");", "\t\tdefault: display_msg(\"b\");", "\tend", "end"));
		}

		private static void AddStatements(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1014", "statements: a procedure called without call", "procedure foo(variable a) begin end", "procedure start begin", "\tfoo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1014", "statements: a procedure name alone as a statement", "procedure foo begin end", "procedure start begin", "\tfoo;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1014", "statements: a procedure called without call after then", "procedure foo(variable a) begin end", "procedure start begin", "\tif (1) then foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("statements: call, a value and a delayed call", "procedure foo(variable a) begin return 1; end", "procedure bar begin end", "procedure start begin", "\tvariable x;", "\tcall foo(1);", "\tx := foo(2);", "\tcall bar in 5;", "\tcall bar when 1;", "\tcall bar;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1016", "statements: a delayed call with arguments", "procedure foo(variable a) begin end", "procedure start begin", "\tcall foo(1) in 5;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1016", "statements: a delayed call with empty parentheses", "procedure foo begin end", "procedure start begin", "\tcall foo() in 5;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("statements: a call through a variable", "procedure foo begin end", "procedure start begin", "\tvariable p;", "\tp := foo;", "\tcall p;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: assignment to a macro with a literal", "#define X 5", "procedure start begin", "\tX := 3;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: assignment to a macro with a parenthesized literal", "#define X (5)", "procedure start begin", "\tX := 3;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: increment of a macro with a literal", "#define X 5", "procedure start begin", "\tX++;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: assignment to a literal", "procedure start begin", "\t3 := 3;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: assignment to a string", "procedure start begin", "\t\"a\" := 3;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: assignment to an engine function", "procedure start begin", "\tdude_obj := 1;", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL1007", "statements: assignment to a constant of the compiler", "procedure start begin", "\ttrue := 0;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("statements: assignment to a macro that is a variable", "variable v;", "#define X v", "procedure start begin", "\tX := 3;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("statements: assignment to an element and a key", "procedure start begin", "\tvariable a;", "\ta := create_array(3, 0);", "\ta[1] := 2;", "\ta.key := 3;", "end"));
		}

		private static void AddFunctions(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2001", "functions: display_msg without argument", "procedure start begin", "\tdisplay_msg();", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2001", "functions: display_msg with two arguments", "procedure start begin", "\tdisplay_msg(\"a\", \"b\");", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2001", "functions: get_critter_stat with one argument", "procedure start begin", "\tvariable x;", "\tx := get_critter_stat(dude_obj);", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2001", "functions: get_critter_stat with three arguments", "procedure start begin", "\tvariable x;", "\tx := get_critter_stat(dude_obj, 1, 2);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("functions: a call that matches its description", "procedure start begin", "\tvariable x;", "\tx := get_critter_stat(dude_obj, 1);", "\tdisplay_msg(\"a\");", "\tx := random(1, 6);", "end"));
			cases.Add(ScriptDiagnosticCase.Warned("SSL2002", "functions: a string where a number is expected", "procedure start begin", "\tvariable x;", "\tx := get_critter_stat(dude_obj, \"a\");", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2004", "functions: dude_obj with parentheses", "procedure start begin", "\tvariable x;", "\tx := dude_obj();", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2004", "functions: sfall_ver_major with parentheses", "procedure start begin", "\tvariable x;", "\tx := sfall_ver_major();", "end"));
			cases.Add(ScriptDiagnosticCase.Rejected("SSL2004", "functions: script_overrides with parentheses", "procedure start begin", "\tscript_overrides();", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("functions: functions without arguments are written bare", "procedure start begin", "\tvariable x;", "\tx := dude_obj;", "\tx := sfall_ver_major;", "\tscript_overrides;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("functions: names written in another case", "procedure start begin", "\tDisplay_Msg(\"a\");", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("functions: a user procedure hides nothing of the catalog", "procedure my_display(variable text) begin display_msg(text); end", "procedure start begin", "\tcall my_display(\"a\");", "end"));
		}

		private static void AddConditional(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Accepted("conditional: a procedure defined in both branches", "#ifdef XX", "procedure foo begin end", "#else", "procedure foo begin end", "#endif", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("conditional: blocks opened in both branches", "procedure start begin", "#ifdef XX", "\tif (1) then begin", "#else", "\tif (2) then begin", "#endif", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("conditional: a name declared in the other branch", "#ifdef XX", "variable a;", "#else", "variable b;", "#endif", "procedure start begin", "\tb := 1;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("conditional: code after #if 0 is not compiled", "#if 0", "procedure start begin", "\tundefined_thing(1);", "#endif", "procedure start begin end"));
			cases.Add(ScriptDiagnosticCase.Accepted("conditional: a definition in the only branch of #if 0 else", "#if 0", "procedure foo begin end", "#else", "procedure foo(variable a) begin end", "#endif", "procedure start begin", "\tcall foo(1);", "end"));
		}

		private static void AddMacros(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Accepted("macros: a macro that declares a variable", "#define DECLARE_TMP variable tmp;", "procedure start begin", "\tDECLARE_TMP", "\ttmp := 1;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("macros: a macro that opens a block", "#define OPEN begin", "procedure start begin", "\tOPEN", "\tdisplay_msg(\"a\");", "\tend", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("macros: a macro with arguments used as a statement", "#define SHOW(x) display_msg(x)", "procedure start begin", "\tSHOW(\"a\");", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("macros: a macro with the name of a call", "#define my_call(a, b) display_msg(a + b)", "procedure start begin", "\tmy_call(\"a\", \"b\");", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("macros: a macro that stands for an unknown name", "#define ALIAS dude_obj", "procedure start begin", "\tvariable x;", "\tx := ALIAS;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("macros: a macro that defines a procedure", "#define MAKE_PROC(n) procedure n begin end", "MAKE_PROC(foo)", "procedure start begin", "\tcall foo;", "end"));
		}

		private static void AddAccepted(List<ScriptDiagnosticCase> cases)
		{
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: an empty script", String.Empty));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: a missing return is not an error", "procedure foo(variable a) begin", "\tvariable b;", "\tb := a;", "end", "procedure start begin", "\tvariable x;", "\tx := foo(1);", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: return with and without a value", "procedure foo begin return; end", "procedure start begin return 1; end"));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: start used as a value", "procedure foo begin end", "procedure start begin", "\tvariable x;", "\tx := start;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: an array element declared locally", "procedure start begin", "\tvariable arr[5];", "\tarr[0] := 1;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: multiple declarations in one statement", "procedure start begin", "\tvariable a, b, c;", "\ta := 1; b := 2; c := 3;", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: a macro used only in a directive", "#define X Y", "procedure start begin", "end"));
			cases.Add(ScriptDiagnosticCase.Accepted("accepted: numbers in every notation", "procedure start begin", "\tvariable a;", "\ta := 0x1F + 1.5 + 10;", "\ta := -1;", "end"));
		}

		/// <summary>Gets the cases as theory data: only the name is passed, so that the test explorer shows a readable list.</summary>
		public static IEnumerable<Object[]> Names(Func<ScriptDiagnosticCase, Boolean> filter)
		{
			return ScriptDiagnosticCase.All.Where(filter).Select(c => new Object[] { c.Name });
		}
	}
}