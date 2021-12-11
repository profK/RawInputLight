# RawInputLight

This is an absolutely minimal wrapper around Win32 RawInpuit in order to get Mouse, KB and Joystick/gamepad input.
It uses a hidden background window to collect the information and works no matter what window has focus.

This release is functioning.  I am likely going to add some device ID info the callbacks, which don't have any
yet.

Note the usages provieded in the callback are the full usage, including the high bits that indicate the usage page.
