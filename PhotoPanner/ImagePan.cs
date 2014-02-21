
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreMotion;
using MonoTouch.CoreAnimation;
using System.Drawing;

namespace PhotoPanner
{
	public class ImagePanScrollBarView : UIView {
		CAShapeLayer scrollbarLayer;

		public ImagePanScrollBarView (RectangleF frame, UIEdgeInsets edgeInsets) : base (frame)
		{
			var scrollbarPath = UIBezierPath.Create ();
			scrollbarPath.MoveTo (new PointF (edgeInsets.Left, Bounds.Height - edgeInsets.Bottom));
			scrollbarPath.AddLineTo (new PointF (Bounds.Width - edgeInsets.Right, Bounds.Height - edgeInsets.Bottom));

			var backgroundLayer = new CAShapeLayer () {
				Path = scrollbarPath.CGPath,
				LineWidth = 1,
				StrokeColor = UIColor.White.ColorWithAlpha (.1f).CGColor,
				FillColor = UIColor.Clear.CGColor
			};


			scrollbarLayer = new CAShapeLayer () {
				Path = scrollbarPath.CGPath,
				LineWidth = 1,
				StrokeColor = UIColor.White.CGColor,
				FillColor = UIColor.Clear.CGColor,
				Actions = new NSDictionary ("strokeStart", NSNull.Null, "strokeEnd", NSNull.Null)
			};

			Layer.AddSublayer (backgroundLayer);
			Layer.AddSublayer (scrollbarLayer);
		}

		public void Update (float scrollAmount, float scrollWidth, float scrollableArea)
		{
			scrollbarLayer.StrokeStart = scrollAmount * scrollableArea;
			scrollbarLayer.StrokeEnd = scrollAmount * scrollableArea + scrollWidth;
		}
	}

	public class ImagePanViewController : UIViewController, IUIScrollViewDelegate
	{
		const float MovementSmoothing = 0.3f;
		const float AnimationDuration = 0.3f;
		const float RotationMultiplier = 5f;

		CMMotionManager motionManager;
		CADisplayLink displayLink;
		UIScrollView panningScrollView;
		UIImageView panningImageView;
		ImagePanScrollBarView scrollbarView;

		public bool MotionBasedPanEnabled { get; set; }

		public ImagePanViewController (CMMotionManager motionManager) : base (null, null)
		{
			this.motionManager = motionManager;
			MotionBasedPanEnabled = true;
		}

		protected override void Dispose (bool disposing)
		{
			displayLink.Invalidate ();
			motionManager.StopDeviceMotionUpdates ();

			base.Dispose (disposing);
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			panningScrollView = new UIScrollView (View.Bounds) {
				AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
				BackgroundColor = UIColor.Blue,
				WeakDelegate = this,
				ScrollEnabled = false,
				AlwaysBounceVertical = false,
				MaximumZoomScale = 2f,
			};
			panningScrollView.PinchGestureRecognizer.AddTarget (PinchRecognized);
			View.AddSubview (panningScrollView);

			panningImageView = new UIImageView (View.Bounds) {
				AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
				BackgroundColor = UIColor.Red,
				ContentMode = UIViewContentMode.ScaleAspectFit
			};
			panningScrollView.AddSubview (panningImageView);

			scrollbarView = new ImagePanScrollBarView (View.Bounds, new UIEdgeInsets (0, 10, 50, 10)) {
				AutoresizingMask =  UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
				UserInteractionEnabled = false,
			};
			View.AddSubview (scrollbarView);

			displayLink = CADisplayLink.Create (DisplayLinkUpdate);
			displayLink.AddToRunLoop (NSRunLoop.Main, NSRunLoop.NSRunLoopCommonModes);

			View.AddGestureRecognizer (new UITapGestureRecognizer (ToggleMotionBasedPan));
		}

		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);
			var csize = panningScrollView.ContentSize;
			var bounds = panningScrollView.Bounds;

			panningScrollView.ContentOffset = new PointF ((csize.Width / 2f - bounds.Width) / 2, (csize.Height / 2f - bounds.Height) / 2);
			motionManager.StartDeviceMotionUpdates (NSOperationQueue.MainQueue, (motion, error) => CalculateRotationBasedOn (motion));
		}

		public override bool PrefersStatusBarHidden ()
		{
			return true;
		}

		public void ConfigureWithImage (UIImage image)
		{
			panningImageView.Image = image;
			var zoomScale = MaximumZoomScaleForImage (image);
			panningScrollView.MaximumZoomScale = zoomScale;
			panningScrollView.ZoomScale = zoomScale;
		}

		void CalculateRotationBasedOn (CMDeviceMotion motion)
		{
			if (!MotionBasedPanEnabled)
				return;
			var rrate = motion.RotationRate;
			if (Math.Abs (rrate.y) <= Math.Abs (rrate.x) + Math.Abs (rrate.z))
				return;
			var invertedYRotationRate = rrate.y * -1;
			float zoomScale = MaximumZoomScaleForImage (panningImageView.Image);
			var interpretedXOffset = panningScrollView.ContentOffset.X + invertedYRotationRate * zoomScale * RotationMultiplier;
			var contentOffset = ClampedContentOffsetForHorizontalOffset ((float)interpretedXOffset);

			UIView.Animate (MovementSmoothing, 
				delay: 0, 
				options: UIViewAnimationOptions.BeginFromCurrentState|UIViewAnimationOptions.AllowUserInteraction|UIViewAnimationOptions.CurveEaseOut,
				animation: () => panningScrollView.SetContentOffset (contentOffset, animated: false), completion: null);
		}

		float MaximumZoomScaleForImage (UIImage image)
		{
			var pBounds = panningScrollView.Bounds;
			var iSize = image.Size;

			return (pBounds.Height / pBounds.Width) * (iSize.Width / iSize.Height);
		}

		void DisplayLinkUpdate ()
		{
			var panningImageViewPresentationLayer = panningImageView.Layer.PresentationLayer;
			var panningScrollViewPresentationLayer = panningScrollView.Layer.PresentationLayer;

			if (panningImageViewPresentationLayer == null || panningScrollViewPresentationLayer == null)
				return;

			var horizontalContentOffset = panningScrollViewPresentationLayer.Bounds.X; // MINX

			var contentWidth = panningImageViewPresentationLayer.Frame.Width;
			var visibleWidth = panningScrollView.Bounds.Width;

			var clampedXOffsetAsPercentage = Math.Max (0, Math.Min (1, horizontalContentOffset / (contentWidth - visibleWidth)));

			var scrollBarWidthPercentage = (float)(visibleWidth / contentWidth);
			var scrollableAreaPercentage = 1.0f - scrollBarWidthPercentage;

			scrollbarView.Update (clampedXOffsetAsPercentage, (float)scrollBarWidthPercentage, scrollableAreaPercentage);
		}

		void ToggleMotionBasedPan ()
		{
			var val = MotionBasedPanEnabled;
			if (val)
				MotionBasedPanEnabled = false;
			UIView.Animate (AnimationDuration, () => {
				UpdateViews (!val);
			}, () => {
				if (!val)
					MotionBasedPanEnabled = true;
			});
		}

		void UpdateViews (bool motionBasedPanEnabled)
		{
			float zoomScale = 1;
			if (motionBasedPanEnabled) {
				zoomScale = MaximumZoomScaleForImage (panningImageView.Image);
				panningScrollView.MaximumZoomScale = zoomScale;
			}
			panningScrollView.ZoomScale = zoomScale;
			panningScrollView.ScrollEnabled = !motionBasedPanEnabled;
		}

		PointF ClampedContentOffsetForHorizontalOffset (float horizontalOffset)
		{
			var maximumXOffset = panningScrollView.ContentSize.Width - panningScrollView.Bounds.Width;
			var minimumXOffset = 0f;

			var clampedXOffset = Math.Max(minimumXOffset, Math.Min(horizontalOffset, maximumXOffset));
			var centeredY = panningScrollView.ContentSize.Height / 2f - panningScrollView.Bounds.Height / 2f;

			return new PointF (clampedXOffset, centeredY);

		}

		void PinchRecognized ()
		{
			MotionBasedPanEnabled = false;
			panningScrollView.ScrollEnabled = true;
		}

		[MonoTouch.Foundation.Export ("viewForZoomingInScrollView:")]
		public MonoTouch.UIKit.UIView ViewForZoomingInScrollView (MonoTouch.UIKit.UIScrollView scrollView)
		{
			return panningImageView;
		}

		[MonoTouch.Foundation.Export ("scrollViewDidEndZooming:withView:atScale:")]
		public void ZoomingEnded (MonoTouch.UIKit.UIScrollView scrollView, MonoTouch.UIKit.UIView withView, float atScale)
		{
			scrollView.SetContentOffset (ClampedContentOffsetForHorizontalOffset (scrollView.ContentOffset.X), true);
		}

		[MonoTouch.Foundation.Export ("scrollViewDidEndDragging:willDecelerate:")]
		public void DraggingEnded (MonoTouch.UIKit.UIScrollView scrollView, bool willDecelerate)
		{
			if (!willDecelerate)
				scrollView.SetContentOffset (ClampedContentOffsetForHorizontalOffset (scrollView.ContentOffset.X), true);
		}

		[MonoTouch.Foundation.Export ("scrollViewWillBeginDecelerating:")]
		public void DecelerationStarted (MonoTouch.UIKit.UIScrollView scrollView)
		{
			scrollView.SetContentOffset (ClampedContentOffsetForHorizontalOffset (scrollView.ContentOffset.X), true);
		}
	}
}

