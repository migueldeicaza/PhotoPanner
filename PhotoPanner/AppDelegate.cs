/*
 * AppDelegate.cs: Launches app and shows the sample image panning
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreMotion;

namespace PhotoPanner
{
	public class SampleViewController : UIViewController
	{
		ImagePanViewController panvc;

		public SampleViewController (): base (null,null) {}
		
		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			panvc = new ImagePanViewController (new CMMotionManager ());
			panvc.WillMoveToParentViewController (this);
			AddChildViewController (panvc);
			View.AddSubview (panvc.View);

			panvc.View.Frame = View.Bounds;
			panvc.View.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
			panvc.DidMoveToParentViewController (this);

			var img = UIImage.FromFile ("melbourne.jpg");
			panvc.ConfigureWithImage (img);
		}

		public override UIViewController ChildViewControllerForStatusBarHidden ()
		{
			return panvc;
		}
	}


	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		UIWindow window;

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			window = new UIWindow (UIScreen.MainScreen.Bounds) {
				RootViewController = new SampleViewController (),
				BackgroundColor = UIColor.White
			};
			window.MakeKeyAndVisible ();
			
			return true;
		}


		static void Main (string[] args)
		{
			UIApplication.Main (args, null, "AppDelegate");
		}
	}
}

