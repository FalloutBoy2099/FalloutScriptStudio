namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class ModelTests
	{
		[Fact]
		public void SourceReference_Create_NormalizesTheFilePath()
		{
			SourceReference reference = SourceReference.Create(@"C:\a\..\b\x.ssl", 7);

			Assert.Equal(Path.GetFullPath(@"C:\b\x.ssl"), reference.File);
			Assert.Equal(7, reference.Line);
			Assert.Null(SourceReference.Create(null, 1).File);
		}

		[Theory]
		[InlineData(VariableScope.Global, "Variable v := 3")]
		[InlineData(VariableScope.Import, "Imported variable v := 3")]
		[InlineData(VariableScope.Export, "Exported variable v := 3")]
		[InlineData(VariableScope.Local, "Local variable v := 3")]
		public void ScriptVariable_Describes_ItsScopeAndInitialValue(VariableScope scope, String expected)
		{
			var variable = new ScriptVariable("v", scope) { InitialValue = "3", InitialValueKind = ValueKind.Int };

			Assert.Equal(expected, variable.FullDescription);
			Assert.Equal(scope == VariableScope.Local ? SymbolKind.LocalVariable : SymbolKind.GlobalVariable, variable.Kind);
			Assert.Equal(scope == VariableScope.Import, variable.IsImported);
			Assert.Equal(scope == VariableScope.Export, variable.IsExported);
		}

		[Fact]
		public void ScriptProcedure_IsStandard_RecognizesEngineHandlersCaseInsensitively()
		{
			Assert.True(new ScriptProcedure("Look_At_P_Proc").IsStandard);
			Assert.True(new ScriptProcedure("start").IsStandard);
			Assert.False(new ScriptProcedure("my_helper").IsStandard);
		}

		[Fact]
		public void ScriptProcedure_Locations_AreOneBasedWithMinusOneForUnknown()
		{
			var procedure = new ScriptProcedure("p");

			Assert.Equal(-1, procedure.DeclaredLine);
			Assert.Equal(-1, procedure.StartLine);
			Assert.Equal(-1, procedure.EndLine);
			Assert.False(procedure.HasForwardDeclaration);
		}
	}
}