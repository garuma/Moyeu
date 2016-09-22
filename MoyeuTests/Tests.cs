using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.UITest;
using Xamarin.UITest.Android;
using Xamarin.UITest.Queries;

namespace MoyeuTests
{
	[TestFixture]
	public class Tests
	{
		AndroidApp app;

		[SetUp]
		public void BeforeEachTest ()
		{
			app = ConfigureApp
				.Android
				.EnableLocalScreenshots ()
				.StartApp ();
		}

		[Test]
		public void InitialLaunch ()
		{
			app.WaitForElement (q => q.Id ("FlashBarText"));
			app.WaitForNoElement (q => q.Id ("FlashBarText"),
			                      postTimeout: TimeSpan.FromSeconds (1));
			app.Screenshot ("Loaded map");
		}

		[Test]
		public void SwitchToFavorites_Empty ()
		{
			app.Tap (q => q.Id ("toolbar").Class ("ImageButton"));
			app.WaitForElement (q => q.Id ("left_drawer"),
			                    postTimeout: TimeSpan.FromSeconds (1));
			app.Tap (q => q.Id ("left_drawer").Descendant ().Text ("Favorites"));
			app.WaitForNoElement (q => q.Id ("left_drawer"),
			                      postTimeout: TimeSpan.FromSeconds (1));
			app.Screenshot ("Empty favorites");
		}

		[Test]
		public void SwitchToFavorites_WithTwoFavs ()
		{
			app.Invoke ("reportFullyDrawn");
			app.Tap (q => q.Id ("toolbar").Class ("ImageButton"));
			app.WaitForElement (q => q.Id ("left_drawer"),
			                    postTimeout: TimeSpan.FromSeconds (1));
			app.Tap (q => q.Id ("left_drawer").Descendant ().Text ("Favorites"));
			app.WaitForNoElement (q => q.Id ("left_drawer"),
			                      postTimeout: TimeSpan.FromSeconds (1));
			app.Screenshot ("Two favorites");
		}

		[Test]
		public void SwitchToRentals ()
		{
			app.Tap (q => q.Id ("toolbar").Class ("ImageButton"));
			app.WaitForElement (q => q.Id ("left_drawer"),
			                    postTimeout: TimeSpan.FromSeconds (1));
			app.Tap (q => q.Id ("left_drawer").Descendant ().Text ("Rental History"));
			app.WaitForNoElement (q => q.Id ("left_drawer"),
			                      postTimeout: TimeSpan.FromSeconds (1));
			app.WaitForElement (q => q.Id ("username"));
			app.Screenshot ("Login form");
			app.EnterText (q => q.Id ("username"), "*");
			app.EnterText (q => q.Id ("password"), "*");
			app.Tap (q => q.Button ("loginBtn"));
			app.WaitForElement (q => q.Id ("loading_icon"));
			app.Screenshot ("Loading screen");
			app.WaitForNoElement (q => q.Id ("loading_icon"),
			                      postTimeout: TimeSpan.FromMilliseconds (500));
			app.Screenshot ("Loaded rentals");
		}
	}
}

