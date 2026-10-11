namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class AboutViewModelTests
	{
		[Theory]
		[InlineData("1.2.3", "Version 1.2.3")]
		[InlineData("1.2.3+abcdef0", "Version 1.2.3")]
		[InlineData("  2026.10.11  ", "Version 2026.10.11")]
		[InlineData("", "Version unknown")]
		[InlineData(null, "Version unknown")]
		public void FormatVersion_CutsBuildMetadata(String? raw, String expected)
		{
			Assert.Equal(expected, AboutViewModel.FormatVersion(raw));
		}

		[Fact]
		public void Content_NamesTheApplicationAuthorAndLicense()
		{
			AboutViewModel viewModel = new AboutViewModel();

			Assert.Equal("Fallout Script Studio", viewModel.ProductName);
			Assert.Equal("by Kain", viewModel.AuthorLine);
			Assert.Contains(".NET 10", viewModel.Description, StringComparison.Ordinal);
			Assert.Contains("Sfall Script Editor", viewModel.OriginalWork, StringComparison.Ordinal);
			Assert.Contains("GNU General Public License", viewModel.License, StringComparison.Ordinal);
			Assert.Contains("version 3", viewModel.License, StringComparison.Ordinal);
			Assert.StartsWith("Version ", viewModel.Version, StringComparison.Ordinal);
			Assert.Contains(viewModel.Credits, credit => credit.Name.Contains("int2ssl", StringComparison.Ordinal));
			Assert.All(viewModel.Links, link => Assert.True(link.Url.IsAbsoluteUri));
		}

		[Fact]
		public void Close_RequestsClose()
		{
			AboutViewModel viewModel = new AboutViewModel();
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CloseCommand.Execute(null);

			Assert.True(closed);
		}
	}
}
