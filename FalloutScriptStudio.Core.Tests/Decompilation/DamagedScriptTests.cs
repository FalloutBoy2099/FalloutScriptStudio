namespace FalloutScriptStudio.Core.Tests.Decompilation
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Decompilation;
	using Xunit;

	public class DamagedScriptTests
	{
		[Theory]
		[InlineData(typeof(NullReferenceException))]
		[InlineData(typeof(FormatException))]
		[InlineData(typeof(ArgumentOutOfRangeException))]
		[InlineData(typeof(IndexOutOfRangeException))]
		[InlineData(typeof(InvalidOperationException))]
		[InlineData(typeof(OverflowException))]
		[InlineData(typeof(InvalidCastException))]
		[InlineData(typeof(KeyNotFoundException))]
		[InlineData(typeof(ArithmeticException))]
		public void AnyFailureOfTheAnalysis_IsASymptomOfADamagedScript(Type type)
		{
			Assert.True(ScriptDecompiler.IsDamageSymptom((Exception)Activator.CreateInstance(type)!));
		}

		[Fact]
		public void TheExceptionOfTheFormatAndTheLackOfMemory_AreLeftAlone()
		{
			Assert.False(ScriptDecompiler.IsDamageSymptom(new ScriptFormatException("x")));
			Assert.False(ScriptDecompiler.IsDamageSymptom(new OutOfMemoryException()));
		}

		[Fact]
		public void Decompile_ManyDamagedScripts_FailOnlyWithTheExceptionOfTheFormat()
		{
			// Whichever way the analysis of a damaged script fails (a missing node, a bad number, an index out of range...), the caller
			// gets a ScriptFormatException, never an exception that would crash the UI.
			List<String> unexpected = [];
			for (Int32 seed = 1; seed <= 25; seed++)
			{
				Random random = new Random(seed);
				foreach (Byte[] original in new[] { CompiledScriptSamples.LoadMap, CompiledScriptSamples.Sample, CompiledScriptSamples.Arrays })
				{
					for (Int32 round = 0; round < 150; round++)
					{
						Byte[] data = (Byte[])original.Clone();
						if (round % 4 == 0)
						{
							data = data[..random.Next(data.Length)];
						}
						else
						{
							for (Int32 index = 0; index < 1 + random.Next(6); index++)
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
						catch (Exception exception) when (exception is not OutOfMemoryException)
						{
							unexpected.Add(exception.GetType().Name);
						}
					}
				}
			}

			Assert.Empty(unexpected);
		}
	}
}