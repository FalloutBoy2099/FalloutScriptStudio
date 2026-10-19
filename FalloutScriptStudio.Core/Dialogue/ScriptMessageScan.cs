namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>What <see cref="ScriptMessageScanner"/> found in a script: the message numbers it uses.</summary>
	/// <param name="Usages">Every message number the script uses (a random range gives one usage per number), in script order.</param>
	/// <param name="UnresolvedCount">The number of uses of the message file of the script whose number could not be worked out (a variable, an unknown macro).</param>
	/// <param name="UsesFemaleVariants">Whether the script mentions the gender of the player (a name that contains <c>female</c> or <c>gender</c>).</param>
	public sealed record ScriptMessageScan(IReadOnlyList<ScriptMessageUsage> Usages, Int32 UnresolvedCount, Boolean UsesFemaleVariants)
	{
		/// <summary>Gets the usages of the message file of the script itself.</summary>
		public IEnumerable<ScriptMessageUsage> OwnUsages => this.Usages.Where(usage => usage.IsOwnFile);

		/// <summary>Gets the numbers of the other message files (scripts.lst numbers) the script uses, ascending.</summary>
		public IReadOnlyList<Int32> OtherFileNumbers => this.Usages.Where(usage => !usage.IsOwnFile).Select(usage => usage.FileNumber).Distinct().Order().ToList();
	}
}