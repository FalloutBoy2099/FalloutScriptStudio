namespace FalloutScriptStudio.Workspace
{
	using System;

	/// <summary>The outcome of <see cref="ProjectSessionService.OfferAsync"/>.</summary>
	/// <param name="Kind">What was done.</param>
	/// <param name="Count">The documents opened (<see cref="ProjectSessionOfferKind.Restored"/>) or available (<see cref="ProjectSessionOfferKind.Offered"/>).</param>
	public sealed record ProjectSessionOffer(ProjectSessionOfferKind Kind, Int32 Count);
}