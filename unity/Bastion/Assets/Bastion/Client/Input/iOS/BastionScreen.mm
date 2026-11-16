// The screen's pixels in a point, for the finger's sizes (GameInput.PixelsPerPoint; docs/IOS.md): Unity's Screen.dpi
// doesn't know every device (the simulator's Duo says 326 for an @3x screen), iOS does.
#import <UIKit/UIKit.h>

extern "C" float BastionScreenScale()
{
    return (float)[UIScreen mainScreen].nativeScale;
}
