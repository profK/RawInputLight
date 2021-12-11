# RawInputLight

This is an absolutely minimal wrapper around Win32 RawInpuit in order to get Mouse, KB and Joystick/gamepad input.
It uses a hidden background window to collect the information and works no matter what window has focus.

It is pure C# and, for the most part, depends on the CsWin32 code generator to create the PInvoke stubs on the fly.
A small number of manual PInvokes are defined in  NativeAPY.CS along with the  native glue code, for when
I couldnt coax PInvoke into generating soemthing I needed.

This release is functioning.  I am likely going to add some device ID info the callbacks, which don't have any
yet.

Note the usages provieded in the callback are the full usage, including the high bits that indicate the usage page.
