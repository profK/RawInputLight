using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace RawInputLight;

public static class NativeAPI
{
    static public unsafe bool RegisterInputDevice(HWND windowHandle, ushort usagePage, ushort usage)
    {
        RAWINPUTDEVICE[] device = {new RAWINPUTDEVICE()};
        device[0].usUsagePage = usagePage;
        device[0].usUsage = usage;
        device[0].hwndTarget = windowHandle;
       
        fixed (RAWINPUTDEVICE* devPtr = device)
        {
           return PInvoke.RegisterRawInputDevices(devPtr,
               1, (uint)sizeof(RAWINPUTDEVICE));
        }
         
    }

    static public unsafe HWND OpenWindow()
    {
        WNDCLASSW windowClass = new WNDCLASSW();
        windowClass.hInstance = new HINSTANCE(IntPtr.Zero); // make null
        fixed (char* p1 = "RAWINPUTWINDOWCLASS")
        {
            PCWSTR classnameStr = new PCWSTR(p1);
            windowClass.lpszClassName = classnameStr;
            windowClass.lpfnWndProc = LpfnWndProc;
            PInvoke.RegisterClass(windowClass);

            fixed (char* p2 = "RAWINPUTWINDOW")
            {
                PCWSTR windownameStr = new PCWSTR(p2);
                return PInvoke.CreateWindowEx(0,classnameStr,windownameStr,0,
                    0,0,0,0,new HWND(0),new HMENU(IntPtr.Zero),
                    windowClass.hInstance);
            }
            
        }
        
    }

    private static LRESULT LpfnWndProc(HWND param0, uint param1, WPARAM param2, LPARAM param3)
    {
        return new LRESULT();
    }
}