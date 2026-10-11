namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Runtime.CompilerServices;
	using FalloutScriptStudio.Localization;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class LanguageChangedSubscriptionTests
	{
		[Fact]
		public void The_callback_receives_the_target_after_a_language_change()
		{
			LanguageCollection.CollectGarbage();
			Subscriber subscriber = new Subscriber();
			using LanguageChangedSubscription subscription = LanguageChangedSubscription.Subscribe(subscriber, static target => target.Changes++);

			Loc.SetLanguage(Loc.DefaultLanguage);
			Loc.SetLanguage(Loc.DefaultLanguage);

			Assert.Equal(2, subscriber.Changes);
			Assert.True(subscription.IsActive);
			GC.KeepAlive(subscriber);
		}

		[Fact]
		public void A_disposed_subscription_is_not_called_any_more()
		{
			LanguageCollection.CollectGarbage();
			Subscriber subscriber = new Subscriber();
			LanguageChangedSubscription subscription = LanguageChangedSubscription.Subscribe(subscriber, static target => target.Changes++);

			subscription.Dispose();
			subscription.Dispose();
			Loc.SetLanguage(Loc.DefaultLanguage);

			Assert.Equal(0, subscriber.Changes);
			Assert.False(subscription.IsActive);
			GC.KeepAlive(subscriber);
		}

		[Fact]
		public void A_collected_subscriber_is_not_kept_alive_and_the_subscription_ends()
		{
			LanguageCollection.CollectGarbage();
			(WeakReference reference, LanguageChangedSubscription subscription) = SubscribeShortLived();

			LanguageCollection.CollectGarbage();

			Assert.False(reference.IsAlive);
			Assert.False(subscription.IsActive);

			// The next language change removes the dead subscription from the static event.
			Loc.SetLanguage(Loc.DefaultLanguage);
			Assert.False(subscription.IsActive);
		}

		[Fact]
		public void A_forever_subscription_calls_the_callback_until_it_is_disposed()
		{
			LanguageCollection.CollectGarbage();
			Int32 calls = 0;
			LanguageChangedSubscription subscription = LanguageChangedSubscription.SubscribeForever(() => calls++);

			Loc.SetLanguage(Loc.DefaultLanguage);
			subscription.Dispose();
			Loc.SetLanguage(Loc.DefaultLanguage);

			Assert.Equal(1, calls);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static (WeakReference Reference, LanguageChangedSubscription Subscription) SubscribeShortLived()
		{
			Subscriber subscriber = new Subscriber();
			LanguageChangedSubscription subscription = LanguageChangedSubscription.Subscribe(subscriber, static target => target.Changes++);
			return (new WeakReference(subscriber), subscription);
		}

		private sealed class Subscriber
		{
			public Int32 Changes { get; set; }
		}
	}
}
