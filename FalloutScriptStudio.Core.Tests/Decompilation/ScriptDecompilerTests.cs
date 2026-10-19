namespace FalloutScriptStudio.Core.Tests.Decompilation
{
	using System;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Decompilation;
	using Xunit;

	public sealed class ScriptDecompilerTests
	{
		private static String[] Lines(String text)
		{
			return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
		}

		private static ScriptDecompilerOptions ThreeSpaces()
		{
			return new ScriptDecompilerOptions { Indent = "   " };
		}

		[Fact]
		public void Decompile_RebuildsTheSourceOfAScript()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.LoadMap);

			Assert.Empty(script.Messages);
			String[] lines = Lines(script.Text);
			Assert.Contains("procedure start;", lines);
			Assert.Contains("procedure start", lines);
			Assert.Contains("\tif (script_action == 6) then begin", lines);
			Assert.Contains("\t\tload_map(33, 0);", lines);
		}

		[Fact]
		public void Decompile_IndentsWithWhatTheOptionsSay()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.LoadMap, new ScriptDecompilerOptions { Indent = "  " });

			Assert.Contains("\n  if (script_action == 6) then begin\n", script.Text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
		}

		[Fact]
		public void Decompile_TheSameScriptTwice_GivesTheSameSource()
		{
			Byte[] data = CompiledScriptSamples.LoadMap;

			Assert.Equal(ScriptDecompiler.Decompile(data).Text, ScriptDecompiler.Decompile((Byte[])data.Clone(), new ScriptDecompilerOptions { Game = ScriptGame.Fallout1 }).Text);
			Assert.Equal(ScriptDecompiler.Decompile(data).Text, ScriptDecompiler.DecompileLeniently(data).Text);
		}

		[Fact]
		public void Decompile_WhatIsNotAScript_IsRefused()
		{
			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.Decompile([]));
			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.Decompile(Encoding.ASCII.GetBytes("procedure start begin end")));
			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.DecompileLeniently(new Byte[200]));
		}

		[Fact]
		public void Decompile_ADamagedScript_FailsOnlyWithTheExceptionOfTheFormat()
		{
			Random random = new Random(2024);
			foreach (Byte[] original in new[] { CompiledScriptSamples.LoadMap, CompiledScriptSamples.Sample, CompiledScriptSamples.Arrays })
			{
				for (Int32 round = 0; round < 300; round++)
				{
					Byte[] data = (Byte[])original.Clone();
					if (round % 3 == 0)
					{
						data = data[..random.Next(data.Length)];
					}
					else
					{
						for (Int32 index = 0; index < 1 + random.Next(4); index++)
						{
							data[random.Next(data.Length)] = (Byte)random.Next(256);
						}
					}

					try
					{
						ScriptDecompiler.Decompile(data);
					}
					catch (ScriptFormatException)
					{
						// The only exception a damaged script may cause.
					}
				}
			}
		}

		[Fact]
		public void Decompile_ANumberOfArgumentsThatIsImpossible_IsRefusedAtOnce()
		{
			// The line of the first procedure in the table ends with its number of arguments: the byte after its body offset (0x96).
			Byte[] data = CompiledScriptSamples.LoadMap;
			Int32 body = 0x30 + data.AsSpan(0x30).IndexOf(new Byte[] { 0, 0, 0, 0x96 });
			Assert.True(body > 0x30);
			data[body + 4] = 0x7F;

			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.Decompile(data));
		}

		[Fact]
		public void Decompile_ASampleWithLoopsCallsAndVariables_MatchesInt2Ssl()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.Sample, ThreeSpaces());

			Assert.Equal(CompiledScriptSamples.SampleSource, Lines(script.Text));
			Assert.Empty(script.Messages);
		}

		[Fact]
		public void Decompile_AnExportedVariable_KeepsItsValue()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.Sample, ThreeSpaces());

			Assert.Contains("export variable shared_value := 5;", Lines(script.Text));
		}

		[Fact]
		public void Decompile_ArrayLiterals_AreJoinedWithPlus()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.Arrays, ThreeSpaces());

			Assert.Equal(CompiledScriptSamples.ArraysSource, Lines(script.Text));
		}

		[Fact]
		public void Decompile_ConditionalExpressions_AreRestoredAsExpressions()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.ConditionalExpression, ThreeSpaces());

			Assert.Equal(CompiledScriptSamples.ConditionalExpressionSource, Lines(script.Text));
			Assert.DoesNotContain(script.Messages, message => message.Contains("left in stack", StringComparison.Ordinal));
		}

		[Fact]
		public void Decompile_TheOpcodesOfTheSfall5Compiler_AreKnown()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.Sfall5Opcodes, ThreeSpaces());

			Assert.Equal(CompiledScriptSamples.Sfall5OpcodesSource, Lines(script.Text));
		}

		[Fact]
		public void Decompile_IncrementsAndDecrements_AreWrittenAsStatements()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(CompiledScriptSamples.Sfall5Opcodes, ThreeSpaces());

			String[] lines = Lines(script.Text);
			Assert.Contains("   LVar1++;", lines);
			Assert.Contains("   LVar1--;", lines);
		}

		[Fact]
		public void DecompileLeniently_UsesTheUsualWayFirst_SoTheResultsAreTheSame()
		{
			foreach (Byte[] data in new[] { CompiledScriptSamples.Sample, CompiledScriptSamples.Arrays, CompiledScriptSamples.ConditionalExpression })
			{
				Assert.Equal(ScriptDecompiler.Decompile(data, ThreeSpaces()).Text, ScriptDecompiler.DecompileLeniently(data, ThreeSpaces()).Text);
			}
		}

		[Fact]
		public void Decompile_WithDifferentGames_ProducesTheSameSourceForCommonOpcodes()
		{
			String fallout2 = ScriptDecompiler.Decompile(CompiledScriptSamples.Sample, new ScriptDecompilerOptions { Game = ScriptGame.Fallout2, Indent = "   " }).Text;
			String fallout1 = ScriptDecompiler.Decompile(CompiledScriptSamples.Sample, new ScriptDecompilerOptions { Game = ScriptGame.Fallout1, Indent = "   " }).Text;

			Assert.Equal(fallout2, fallout1);
			Assert.Contains('\n', fallout2);
		}
	}
}