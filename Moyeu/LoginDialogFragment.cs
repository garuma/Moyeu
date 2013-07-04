using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using Runnable = Java.Lang.Runnable;

namespace Moyeu
{
	public class LoginDialogFragment : DialogFragment
	{
		public event EventHandler Dismissed;

		TaskCompletionSource<RentalCrendentials> newCredentials
			= new TaskCompletionSource<RentalCrendentials> ();

		Button loginBtn;
		ProgressBar statusProgress;
		TextView statusText;
		EditText username;
		EditText password;

		public override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			SetStyle (DialogFragmentStyle.NoFrame, Android.Resource.Style.ThemeHoloLightDialogNoActionBar);
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var dialogView = inflater.Inflate (Resource.Layout.LoginDialogLayout, container, false);
			loginBtn = dialogView.FindViewById<Button> (Resource.Id.loginBtn);
			loginBtn.Click += ProcessLoginDetails;
			statusProgress = dialogView.FindViewById<ProgressBar> (Resource.Id.loginStatusProgress);
			statusText = dialogView.FindViewById<TextView> (Resource.Id.loginStatusText);
			username = dialogView.FindViewById<EditText> (Resource.Id.username);
			password = dialogView.FindViewById<EditText> (Resource.Id.password);

			return dialogView;
		}

		void ProcessLoginDetails (object sender, EventArgs e)
		{
			loginBtn.Enabled = false;
			password.Enabled = false;
			username.Enabled = false;

			statusProgress.Alpha = 0;
			statusProgress.Visibility = ViewStates.Visible;
			if (statusText.Visibility == ViewStates.Visible) {
				statusText.Animate ().Alpha (0).WithEndAction (new Runnable (() => {
					statusText.Visibility = ViewStates.Gone;
					statusProgress.Animate ().Alpha (1).Start ();
				})).Start ();
			} else {
				statusProgress.Animate ().Alpha (1).Start ();
			}

			newCredentials.SetResult (new RentalCrendentials {
				Username = username.Text,
				Password = password.Text
			});
		}

		public void AdvertiseLoginError ()
		{
			newCredentials = new TaskCompletionSource<RentalCrendentials> ();
			password.Text = string.Empty;
			loginBtn.Enabled = true;
			password.Enabled = true;
			username.Enabled = true;

			statusProgress.Animate ().Alpha (0).WithEndAction (new Runnable (() => {
				statusProgress.Visibility = ViewStates.Gone;
				statusText.Alpha = 0;
				statusText.Visibility = ViewStates.Visible;
				statusText.Animate ().Alpha (1).Start ();
			})).Start ();
		}

		public Task<RentalCrendentials> GetCredentialsAsync ()
		{
			return newCredentials.Task;
		}

		public override void OnDismiss (IDialogInterface dialog)
		{
			base.OnDismiss (dialog);
			if (Dismissed != null)
				Dismissed (this, EventArgs.Empty);
		}
	}
}

