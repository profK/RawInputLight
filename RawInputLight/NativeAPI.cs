using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Win32;
using static Windows.Win32.PInvoke;

namespace RawInputLight;


public static class NativeAPI
{
    const uint UINT_MINUS_ONE = 0XFFFFFFFF;
    
    public static event Action<HANDLE, ushort, KeyState> KeyListeners;
    public static event Action<HANDLE, int, int, uint, int> MouseStateListeners;
    public static event Action<HANDLE, uint, bool[]> ButtonDownListeners;
    public static event Action<HANDLE, uint[], uint[]> AxisListeners;

    private static ConcurrentDictionary<HANDLE, DeviceInfo> deviceInfo = 
        new ConcurrentDictionary<HANDLE, DeviceInfo>();

    public static uint LastError = 0;
    
    public static unsafe void RefreshDeviceInfo()
    {
        deviceInfo.Clear();
        uint numDevices = 0;
        if (GetRawInputDeviceList((RAWINPUTDEVICELIST*)IntPtr.Zero.ToPointer(),
                &numDevices,
                (uint)sizeof(RAWINPUTDEVICELIST)) == UINT_MINUS_ONE)
        {
            LastError = GetLastError();
            return;
        }
        IntPtr devListPtr = Marshal.AllocHGlobal(
            (int) numDevices * sizeof(RAWINPUTDEVICELIST));
        if (GetRawInputDeviceList((RAWINPUTDEVICELIST*)devListPtr.ToPointer(),
                &numDevices,
                (uint)sizeof(RAWINPUTDEVICELIST)) == UINT_MINUS_ONE)
        {
            LastError = GetLastError();
            return;
        };

        for (int i = 0; i < numDevices; i++)
        {
            IntPtr recPtr = IntPtr.Add(devListPtr,
                i * sizeof(RAWINPUTDEVICELIST));
            RAWINPUTDEVICELIST rec = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(recPtr);
            if ((rec.dwType == RID_DEVICE_INFO_TYPE.RIM_TYPEMOUSE) ||
                (rec.dwType == RID_DEVICE_INFO_TYPE.RIM_TYPEKEYBOARD) /*||
                (rec.dwType == RID_DEVICE_INFO_TYPE.RIM_TYPEHID)*/  )
            {
                deviceInfo.AddOrUpdate(rec.hDevice, new DeviceInfo(rec),
                    (handle, info) => info);
            }
        }
        Marshal.FreeHGlobal(devListPtr);
    }

    public static DeviceInfo[] GetDevices()
    {
        return deviceInfo.Values.ToArray();
    }
    
    public struct HID_DEV_ID
    {
        public ushort page;
        public ushort usage;

        public HID_DEV_ID(ushort p, ushort u)
        {
            page = p;
            usage = u;
        }
    }

    public static unsafe bool RegisterInputDevices(HWND windowHandle, HID_DEV_ID[] devices)
    {
        RAWINPUTDEVICE[] rawDevices = new RAWINPUTDEVICE[devices.Length];
        for (int i = 0; i < rawDevices.Length; i++)
        {

            rawDevices[i].usUsagePage = devices[i].page;
            rawDevices[i].usUsage = devices[i].usage;
            rawDevices[i].hwndTarget = windowHandle;
            rawDevices[i].dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK;
        }

        fixed (RAWINPUTDEVICE* devPtr = rawDevices)
        {
            if (RegisterRawInputDevices(devPtr,
                    (uint) rawDevices.Length, (uint) sizeof(RAWINPUTDEVICE)))
            {
                return true;
            }
            else
            {
                Console.WriteLine("Error registering raw device: " + GetLastError());
                return false;
            }
        }
    }

    public static void MessagePump(HWND_WRAPPER hwndWrapper)
    {

        MSG msg;
        bool done = false;
        do
        {
            int result = GetMessage(out msg, hwndWrapper.hwnd, 0, 0);
            switch (result)
            {
                case -1: //error
                    Console.WriteLine("Message Pump Error: " + GetLastError());
                    break;
                case 0: //quit
                    done = true;
                    break;
                default:
                    // Console.WriteLine("Pumping message: "+(WindowsMessages)msg.message);
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                    break;
            }

        } while (!done);

    }

    private static WNDPROC wndProc = new WNDPROC(LpfnWndProc);
    public static unsafe HWND_WRAPPER OpenWindow()
    {
        var windowClass = new WNDCLASSW();
        var hinstance = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
        windowClass.hInstance = new HINSTANCE(hinstance); // make null
        fixed (char* p1 = "RAWINPUTWINDOWCLASS")
        {
            var classnameStr = new PCWSTR(p1);
            windowClass.lpszClassName = classnameStr;
            windowClass.lpfnWndProc = wndProc;
            var result = RegisterClass(windowClass);
            if (result == 0) throw new Exception("Registering window class failed");

            fixed (char* p2 = "")
            {
                var windownameStr = new PCWSTR(p2);
                var hwnd = CreateWindowEx(0, classnameStr, windownameStr,
                    0,
                    0, 0, 0, 0, new HWND(0), new HMENU(IntPtr.Zero),
                    windowClass.hInstance, null);

                if (hwnd.Value == IntPtr.Zero)
                    throw new Exception("Opening window failed: " +
                                        GetLastError());
                ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_NORMAL);
                //PInvoke.SetCapture(hwnd);
                return new HWND_WRAPPER(hwnd);
            }
        }
    }




    //seem to need to add these manually
    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, WPARAM wParam, LPARAM lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public static implicit operator System.Drawing.Point(POINT p)
        {
            return new System.Drawing.Point(p.X, p.Y);
        }

        public static implicit operator POINT(System.Drawing.Point p)
        {
            return new POINT(p.X, p.Y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]

    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public int time;
        public POINT pt;
        public int lPrivate;
    }

    [DllImport("user32.dll")]
    static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

    public static DeviceInfo? GetDeviceInfo(HANDLE dHandle)
    {
        if (!deviceInfo.ContainsKey(dHandle))
        {
            RefreshDeviceInfo();
            if (!deviceInfo.ContainsKey(dHandle))
            {
                return null;
            }
        }

        return deviceInfo[dHandle];
    }

    public struct DeviceNames
    {
        public string devPath;
        public string Manufacturer;
        public string Product;

        public DeviceNames(string path, string manufacturer, string product)
        {
            devPath = path;
            Manufacturer = manufacturer;
            Product = product;
        }
    }

    
    private static unsafe LRESULT LpfnWndProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        
        //Console.WriteLine("Recvd Message: "+(WindowsMessages)uMsg);
        switch (uMsg)
        {
            case (uint)WindowsMessages.WM_NCCALCSIZE:
            {
                if (wParam != 0) return new LRESULT(0);
                break;
            }
            
            case (uint)WindowsMessages.WM_INPUT: //WM_INPUT: dsfs
            {
                //Console.WriteLine("Processing input message");
                uint dwSize;
                
                if (lParam == 0)
                {
                    return new LRESULT(0);
                }


                GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                    IntPtr.Zero.ToPointer(), &dwSize, (uint) sizeof(RAWINPUTHEADER));

                if (dwSize == 0) return new LRESULT(0);

                var lpb = Marshal.AllocHGlobal((int) dwSize);
                if (GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                        lpb.ToPointer(), &dwSize, (uint) sizeof(RAWINPUTHEADER)) != dwSize)
                    Console.WriteLine("GetRawInputData does not return correct size !\n");

                var raw = (RAWINPUT*) lpb;
                HANDLE devHandle =raw->header.hDevice;
                
                if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEKEYBOARD)
                {
                    switch (raw->data.keyboard.Message)
                    {
                        case (int) WindowsMessages.WM_KEYDOWN:
                            string name = "";
                            KeyListeners?.Invoke(devHandle,raw->data.keyboard.VKey, KeyState.KeyDown);
                            break;
                        case (int) WindowsMessages.WM_KEYUP:
                            name = "";
                            KeyListeners?.Invoke(devHandle,raw->data.keyboard.VKey, KeyState.KeyUp);
                            break;
                    }
                } 
                else if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEMOUSE)
                {
                  
                    MouseStateListeners?.Invoke(devHandle,raw->data.mouse.lLastX,
                        raw->data.mouse.lLastY, raw->data.mouse.Anonymous.Anonymous.usButtonFlags,
                        raw->data.mouse.Anonymous.Anonymous.usButtonData);
                }
                else if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEHID)
                {
                   
                    ParseRawHID(raw);
                }

                Marshal.FreeHGlobal(lpb);

                return new LRESULT(0);
            }
            default:
                //Console.WriteLine("Default windproc proceessing");
                return new LRESULT(DefWindowProc(hWnd, uMsg, wParam, lParam));
        }

        return new LRESULT(0);
    }

 
   
    

    private static unsafe void ParseRawHID(RAWINPUT* raw)
    {
        DeviceInfo? devInfo = GetDeviceInfo(raw->header.hDevice);
        if (devInfo.HasValue)
            using (PreparsedData ppd = new PreparsedData(raw->header.hDevice)){
                uint usageMin = 0;
                uint usageMax = 0;
                uint numUsages = 0;
                HIDP_BUTTON_CAPS firstCaps = devInfo.Value.ButtonCaps[0];
                if (firstCaps.IsRange.Value != 0)
                {
                    usageMin = (uint) (firstCaps.Anonymous.Range.UsageMin & 0x00FF);
                    usageMax = (uint) (firstCaps.Anonymous.Range.UsageMax & 0x00FF);
                    numUsages = (usageMax - usageMin) + 1;
                }
                else
                {
                    usageMin = firstCaps.Anonymous.NotRange.Usage;
                    usageMax = usageMin;
                    numUsages = 1;
                }

                var usagesPtr = Marshal.AllocHGlobal((int) numUsages * sizeof(ushort));
                uint reportedUsages = numUsages;
                fixed (byte* report = &(raw->data.hid.bRawData[0]))
                {
                    HidP_GetUsages(
                        HIDP_REPORT_TYPE.HidP_Input, firstCaps.UsagePage, 0,
                        (ushort*) usagesPtr.ToPointer(), ref reportedUsages,
                        ppd,
                        new PSTR(report), raw->data.hid.dwSizeHid);
                }

                //process usages
                bool[] usageStates = new bool[numUsages]; // total length
                Array.Fill(usageStates, false, 0, (int) numUsages);
                for (int i = 0; i < reportedUsages; i++)
                {
                    ushort* ushortPtr = (ushort*)
                        IntPtr.Add(usagesPtr, i * sizeof(ushort)).ToPointer();
                    uint usage = *ushortPtr;
                    usageStates[usage - firstCaps.Anonymous.Range.UsageMin] = true;
                }

                var compositeUsage = ((uint) firstCaps.UsagePage << 16) |
                                     firstCaps.Anonymous.Range.UsageMin;
                ButtonDownListeners?.Invoke(raw->header.hDevice, compositeUsage,
                    usageStates);
               
                
                //get values
                uint[] values = new uint[devInfo.Value.ValueCaps.Length];
                uint[] usages = new uint[devInfo.Value.ValueCaps.Length];
                fixed (byte* report = &(raw->data.hid.bRawData[0]))
                {

                    for (int i = 0; i < devInfo.Value.ValueCaps.Length; i++)
                    {
                        HIDP_VALUE_CAPS vcaps =
                            devInfo.Value.ValueCaps[i];
                        if (vcaps.IsRange.Value != 0)
                        {
                            usageMin = (uint) (vcaps.Anonymous.Range.UsageMin & 0x00FF);
                        }
                        else
                        {
                            usageMin = vcaps.Anonymous.NotRange.Usage;
                        }

                        usages[i] = ((uint) vcaps.UsagePage << 16) | usageMin;
                        HidP_GetUsageValue(
                            HIDP_REPORT_TYPE.HidP_Input, vcaps.UsagePage, 0,
                            (ushort) usageMin, out values[i],
                            ppd,
                            new PSTR(report), raw->data.hid.dwSizeHid);
                    }
                }
                AxisListeners?.Invoke(raw->header.hDevice,usages, values);
                Marshal.FreeHGlobal(usagesPtr);
            }
    }

    public struct HWND_WRAPPER
    {
        public HWND hwnd;

        public HWND_WRAPPER(HWND h)
        {
            hwnd = h;
        }
    }

    private enum RAW_INPUT_TYPE
    {
        RIM_TYPEMOUSE = 0,
        RIM_TYPEKEYBOARD = 1,
        RIM_TYPEHID = 2
    }
}