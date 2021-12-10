using Windows.Win32.Foundation;

namespace RawInputLight;

public class RawInput
{
    private const ushort GenericDesktopPage = 0x01;
    private const ushort GenericMouse = 0x02;
    private const ushort GenericJoystick = 0x04;
    private const ushort GenericGamepad = 0x05;
    private const ushort GenericKeyboard = 0x06;

    public RawInput(NativeAPI.HWND_WRAPPER wrapper) : this(wrapper.hwnd)
    {
        //nop
    }

    public RawInput(HWND windowHandle)
    {
        NativeAPI.RegisterInputDevices(windowHandle, new NativeAPI.HID_DEV_ID[]
        {
            new NativeAPI.HID_DEV_ID(GenericDesktopPage, GenericMouse),
            new NativeAPI.HID_DEV_ID(GenericDesktopPage, GenericGamepad),
            new NativeAPI.HID_DEV_ID(GenericDesktopPage, GenericJoystick),
            new NativeAPI.HID_DEV_ID(GenericDesktopPage, GenericKeyboard)
        });
        
    }
}