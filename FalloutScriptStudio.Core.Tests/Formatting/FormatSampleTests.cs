namespace FalloutScriptStudio.Core.Tests.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.Tests.Native;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	/// <summary>The formatter over the real scripts of FO2tweaksMk2: defaults, stability, and "only white space changes".</summary>
	// Heavy on the processor: it runs with the performance tests, never beside them, so that their timings stay meaningful.
	[Xunit.Collection(FalloutScriptStudio.Core.Tests.Performance.PerformanceCollection.Name)]
	public sealed class FormatSampleTests
	{
		private static readonly CodeFormatter _formatter = new CodeFormatter();

		public static IEnumerable<Object[]> SetNames()
		{
			return FormatSamples.SetNames();
		}

		[RealToolFact(RealTools.Fo2TweaksSamples)]
		public void DefaultOptions_ReproduceTheOriginalFormatter_OnEverySample()
		{
			LegacyCodeFormatter legacy = new LegacyCodeFormatter();
			IReadOnlyList<(String Name, String Text)> files = FormatSamples.Fo2Tweaks();
			Assert.NotEmpty(files);

			foreach ((String name, String text) in files)
			{
				Assert.True(String.Equals(legacy.Format(text), _formatter.Format(text), StringComparison.Ordinal), name);
				String mutated = FormatSamples.Mutate(text, 7);
				Assert.True(String.Equals(legacy.Format(mutated), _formatter.Format(mutated), StringComparison.Ordinal), name + " (mutated)");
			}
		}

		[Fact]
		public void DefaultOptions_ReproduceTheOriginalFormatter_OnGeneratedOperatorSoup()
		{
			LegacyCodeFormatter legacy = new LegacyCodeFormatter();
			Random random = new Random(1234);
			String[] pieces = { "a", "b1", "_c", "0x1F", "12", "1.5", "+", "-", "*", "/", "%", "<", ">", "<=", ">=", "==", "!=", ":=", "+=", "-=", "++", "--", ",", "(", ")", " ", "  ", "\t", "\"s+t,u\"", "// c+d,e", "/* f+g */", "/*", "*/", ";" };
			for (Int32 i = 0; i < 3000; i++)
			{
				System.Text.StringBuilder text = new System.Text.StringBuilder();
				Int32 count = random.Next(1, 14);
				for (Int32 j = 0; j < count; j++)
				{
					text.Append(pieces[random.Next(pieces.Length)]);
					if (random.Next(10) == 0)
					{
						text.Append(random.Next(2) == 0 ? "\r\n" : "\n");
					}
				}

				String input = text.ToString();
				Assert.True(String.Equals(legacy.Format(input), _formatter.Format(input), StringComparison.Ordinal), "Input: " + input.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal));
			}
		}

		[RealToolTheory(RealTools.Fo2TweaksSamples)]
		[MemberData(nameof(SetNames))]
		public void Formatting_ChangesOnlyWhiteSpace_AndFormattingTwiceIsFormattingOnce(String setName)
		{
			FormatOptions options = FormatSamples.Sets[setName];
			foreach ((String name, String text) in FormatSamples.Fo2Tweaks())
			{
				foreach (String input in new[] { text, FormatSamples.Mutate(text, 11), FormatSamples.Mutate(text, 12) })
				{
					String once = _formatter.Format(input, options);
					Assert.True(String.Equals(FormatSamples.Signature(input), FormatSamples.Signature(once), StringComparison.Ordinal), setName + ": " + name + " changed its tokens");
					String twice = _formatter.Format(once, options);
					Assert.True(String.Equals(once, twice, StringComparison.Ordinal), setName + ": " + name + " is not stable" + Describe(once, twice));
				}
			}
		}

		[RealToolTheory(RealTools.Fo2TweaksSamples)]
		[MemberData(nameof(SetNames))]
		public void Formatting_CutDownScriptsStayStable_EvenWhenTheyEndInTheMiddleOfAConstruct(String setName)
		{
			FormatOptions options = FormatSamples.Sets[setName];
			Random random = new Random(99);
			foreach ((String name, String text) in FormatSamples.Fo2Tweaks().Where(file => file.Name.EndsWith(".ssl", StringComparison.Ordinal)).Take(6))
			{
				for (Int32 i = 0; i < 6; i++)
				{
					Int32 start = random.Next(0, text.Length / 2);
					String cut = text.Substring(start, random.Next(Math.Min(200, text.Length - start), Math.Min(4000, text.Length - start) + 1));
					String once = _formatter.Format(cut, options);
					Assert.True(String.Equals(FormatSamples.Signature(cut), FormatSamples.Signature(once), StringComparison.Ordinal), setName + ": " + name + " cut changed its tokens");
					Assert.True(String.Equals(once, _formatter.Format(once, options), StringComparison.Ordinal), setName + ": " + name + " cut is not stable" + Describe(once, _formatter.Format(once, options)));
				}
			}
		}

		[Theory]
		[MemberData(nameof(SetNames))]
		public void TheSampleOfThePreviewIsStable_AndChangesOnlyWhiteSpace(String setName)
		{
			FormatOptions options = FormatSamples.Sets[setName];

			String once = _formatter.Format(FormatPreviewSample.Text, options);

			Assert.Equal(FormatSamples.Signature(FormatPreviewSample.Text), FormatSamples.Signature(once));
			Assert.Equal(once, _formatter.Format(once, options));
		}

		private static String Describe(String once, String twice)
		{
			String[] a = once.Split('\n');
			String[] b = twice.Split('\n');
			for (Int32 i = 0; i < Math.Min(a.Length, b.Length); i++)
			{
				if (!String.Equals(a[i], b[i], StringComparison.Ordinal))
				{
					return " at line " + (i + 1) + ": [" + a[i].TrimEnd('\r') + "] became [" + b[i].TrimEnd('\r') + "]";
				}
			}

			return " (the line count changed from " + a.Length + " to " + b.Length + ")";
		}
	}
}