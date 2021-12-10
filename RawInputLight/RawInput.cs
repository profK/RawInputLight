﻿using System.Net;
using Windows.Win32.Foundation;

namespace RawInputLight;

public class RawInput
{
    private const ushort GenericDesktopPage = 0x01;
    private const ushort GenericMouse = 0x02;
    private const ushort GenericJoystick = 0x04;
    private const ushort GenericGamepad = 0x05;
    private const ushort GenericKeyboard = 0x06;
    
   

    public Action<ushort, KeyState> KeyStateChangeEvent;
    public Action<int, int, uint,int> MouseStateChangeEvent; 

    public RawInput(NativeAPI.HWND_WRAPPER wrapper) : this(wrapper.hwnd)
    {
        NativeAPI.KeyListeners += (arg1, state) => 
            KeyStateChangeEvent?.Invoke(arg1,state) ;
        NativeAPI.MouseStateListeners += (i, i1, arg3, arg4) =>
            MouseStateChangeEvent?.Invoke(i, i1, arg3, arg4);
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