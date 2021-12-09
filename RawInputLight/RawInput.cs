using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;


namespace RawInputLight
{
    public  class RawInput
    {
        private const ushort GenericDesktopPage = 0x01;
        private const ushort GenericMouse = 0x02;
        private const ushort GenericJoystick = 0x04;
        private const ushort GenericGamepad = 0x05;
        private const ushort GenericKeyboard = 0x06;
        public RawInput(HWND windowHandle)
        {
            NativeAPI.RegisterInputDevice(windowHandle, GenericDesktopPage, GenericMouse);
            NativeAPI.RegisterInputDevice(windowHandle, GenericDesktopPage, GenericJoystick);
            NativeAPI.RegisterInputDevice(windowHandle, GenericDesktopPage, GenericGamepad);
            NativeAPI.RegisterInputDevice(windowHandle, GenericDesktopPage, GenericKeyboard);
        }
    }
}