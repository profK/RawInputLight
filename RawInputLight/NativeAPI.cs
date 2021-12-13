using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;
using HidSharp;
using Microsoft.Win32;

namespace RawInputLight;

public static class NativeAPI
{
    public static Action<HANDLE, ushort, KeyState> KeyListeners;
    public static Action<HANDLE, int, int, uint, int> MouseStateListeners;
    public static Action<HANDLE, uint, bool[]> ButtonDownListeners;
    public static Action<HANDLE, uint[], uint[]> AxisListeners;

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
            if (PInvoke.RegisterRawInputDevices(devPtr,
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

    public static unsafe HWND_WRAPPER OpenWindow()
    {
        var windowClass = new WNDCLASSW();
        var hinstance = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
        windowClass.hInstance = new HINSTANCE(hinstance); // make null
        fixed (char* p1 = "RAWINPUTWINDOWCLASS")
        {
            var classnameStr = new PCWSTR(p1);
            windowClass.lpszClassName = classnameStr;
            windowClass.lpfnWndProc = LpfnWndProc;
            var result = PInvoke.RegisterClass(windowClass);
            if (result == 0) throw new Exception("Registering window class failed");

            fixed (char* p2 = "")
            {
                var windownameStr = new PCWSTR(p2);
                var hwnd = PInvoke.CreateWindowEx(0, classnameStr, windownameStr,
                    0,
                    0, 0, 0, 0, new HWND(0), new HMENU(IntPtr.Zero),
                    windowClass.hInstance, null);

                if (hwnd.Value == IntPtr.Zero)
                    throw new Exception("Opening window failed: " +
                                        GetLastError());
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_NORMAL);
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





    private static Dictionary<HANDLE, DeviceNames> deviceNames = 
        new Dictionary<HANDLE, DeviceNames>();
   
    
    public static unsafe void RefreshDeviceNames()
    {
        deviceNames.Clear();
        uint numDevices = 0;
        PInvoke.GetRawInputDeviceList((RAWINPUTDEVICELIST*) IntPtr.Zero.ToPointer(),
            &numDevices,
            (uint) sizeof(RAWINPUTDEVICELIST));
        IntPtr devListPtr = Marshal.AllocHGlobal(
            (int) numDevices * sizeof(RAWINPUTDEVICELIST));
        PInvoke.GetRawInputDeviceList((RAWINPUTDEVICELIST*) devListPtr.ToPointer(),
            &numDevices,
            (uint) sizeof(RAWINPUTDEVICELIST));

        for (int i = 0; i < numDevices; i++)
        {
            IntPtr recPtr = IntPtr.Add(devListPtr,
                i * sizeof(RAWINPUTDEVICELIST));
            RAWINPUTDEVICELIST rec = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(recPtr);
            uint nameSize = 0;
            PInvoke.GetRawInputDeviceInfo(rec.hDevice, RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_DEVICENAME,
                IntPtr.Zero.ToPointer(), &nameSize);
            IntPtr nameBuffer = Marshal.AllocHGlobal((int) nameSize * 2);
            PInvoke.GetRawInputDeviceInfo(rec.hDevice, RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_DEVICENAME,
                nameBuffer.ToPointer(), &nameSize);
            string name = Marshal.PtrToStringAuto(nameBuffer);
            deviceNames.Add(rec.hDevice, GetGetProductNames(name));
           
            Marshal.FreeHGlobal(nameBuffer);
        }

        Marshal.FreeHGlobal(devListPtr);
    }

    public static DeviceNames? GetDeviceNames(HANDLE dHandle)
    {
        if (!deviceNames.ContainsKey(dHandle))
        {
            RefreshDeviceNames();
            if (!deviceNames.ContainsKey(dHandle))
            {
                return null;
            }
        }

        return deviceNames[dHandle];
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

    

public static DeviceNames GetGetProductNames(string devicePath)
    {
        var path = devicePath.Substring(4).Replace('#', '\\');
        if (path.Contains("{")) path = path.Substring(0, path.IndexOf('{') - 1);

        var device = CfgMgr32.LocateDevNode(path, CfgMgr32.LocateDevNodeFlags.Phantom);

        string manufacturerName = "";
        string productName = "";
        manufacturerName = CfgMgr32.GetDevNodePropertyString(device, in DevicePropertyKey.DeviceManufacturer);
        productName = CfgMgr32.GetDevNodePropertyString(device, in DevicePropertyKey.DeviceFriendlyName);
        productName ??= CfgMgr32.GetDevNodePropertyString(device, in DevicePropertyKey.Name);
        return new DeviceNames(path, manufacturerName, productName);
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
              

                PInvoke.GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                    IntPtr.Zero.ToPointer(), &dwSize, (uint) sizeof(RAWINPUTHEADER));

                if (dwSize == 0) return new LRESULT(0);

                var lpb = Marshal.AllocHGlobal((int) dwSize);
                if (PInvoke.GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
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
        uint dataSize=0;
        PInvoke.GetRawInputDeviceInfo(
            raw->header.hDevice,
            RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_PREPARSEDDATA,
            IntPtr.Zero.ToPointer(), &dataSize);
        if (dataSize == 0)
        {
            Console.WriteLine("No preparsed data");
            return;
        }
        var pPreparsedDataBuffer = Marshal.AllocHGlobal((int)dataSize);
        PInvoke.GetRawInputDeviceInfo(raw->header.hDevice,
            RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_PREPARSEDDATA,
            pPreparsedDataBuffer.ToPointer(),&dataSize);
        //now process preparsed data

        HIDP_CAPS hcaps = new HIDP_CAPS();
        PInvoke.HidP_GetCaps((nint)pPreparsedDataBuffer.ToInt64(),
            out hcaps);
        // get button caps
        var buttonCapsPtr = Marshal.AllocHGlobal(
            sizeof(HIDP_BUTTON_CAPS) * hcaps.NumberInputButtonCaps);
        ushort buttonCapsLength=hcaps.NumberInputButtonCaps;
        var capsLength = hcaps.NumberInputButtonCaps;
        PInvoke.HidP_GetButtonCaps(HIDP_REPORT_TYPE.HidP_Input,
            (HIDP_BUTTON_CAPS *)buttonCapsPtr.ToPointer(), ref buttonCapsLength,
            (nint) pPreparsedDataBuffer.ToInt64());
        // Get buttons down
        HIDP_BUTTON_CAPS* pButtonCaps = (HIDP_BUTTON_CAPS*) buttonCapsPtr.ToPointer();
        uint usageMin = 0;
        uint usageMax = 0;
        uint numUsages = 0;
        if (pButtonCaps->IsRange.Value!=0)
        {
            usageMin = (uint) (pButtonCaps->Anonymous.Range.UsageMin & 0x00FF);
            usageMax = (uint) (pButtonCaps->Anonymous.Range.UsageMax & 0x00FF);
            numUsages = (usageMax - usageMin)+1;
        }
        else
        {
            usageMin = pButtonCaps->Anonymous.NotRange.Usage;
            usageMax = usageMin;
            numUsages = 1;
        }

        var usagesPtr = Marshal.AllocHGlobal((int)numUsages * sizeof(ushort));
        uint reportedUsages = numUsages;
        fixed (byte* report = &(raw->data.hid.bRawData[0]))
        {
            PInvoke.HidP_GetUsages(
                HIDP_REPORT_TYPE.HidP_Input, pButtonCaps->UsagePage, 0, 
                (ushort*)usagesPtr.ToPointer(),ref reportedUsages,
                (nint) (pPreparsedDataBuffer.ToPointer()),
                new PSTR(report), raw->data.hid.dwSizeHid);
        }
        //process usages
        bool[] usageStates = new bool[numUsages]; // total length
        Array.Fill(usageStates,false,0,(int) numUsages);
        for (int i = 0; i < reportedUsages; i++)
        {
            ushort* ushortPtr = (ushort *)
                IntPtr.Add(usagesPtr,i * sizeof(ushort)).ToPointer();
            uint usage = *ushortPtr;
            usageStates[usage-pButtonCaps->Anonymous.Range.UsageMin] = true;
        }

        var compositeUsage = ((uint)pButtonCaps->UsagePage << 16) | 
                     pButtonCaps->Anonymous.Range.UsageMin;
        ButtonDownListeners?.Invoke(raw->header.hDevice,compositeUsage,
            usageStates);
        //get value caps
        var valueCapsPtr = Marshal.AllocHGlobal(
            sizeof(HIDP_VALUE_CAPS) * hcaps.NumberInputValueCaps);
        ushort valueCapsLength=hcaps.NumberInputValueCaps;
        HIDP_VALUE_CAPS* pValueCaps = (HIDP_VALUE_CAPS*) valueCapsPtr.ToPointer();
        PInvoke.HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input,
            pValueCaps, 
            ref valueCapsLength,
            (nint) pPreparsedDataBuffer.ToInt64());
        //get values
        uint[] values = new uint[valueCapsLength];
        uint[] usages = new uint[valueCapsLength];
        fixed (byte* report = &(raw->data.hid.bRawData[0]))
        {
           
            for (int i = 0; i< valueCapsLength; i++)
            {
                HIDP_VALUE_CAPS* vcapPtr =
                    (HIDP_VALUE_CAPS*)IntPtr.Add(valueCapsPtr,
                        i * sizeof(HIDP_VALUE_CAPS)).ToPointer();
                if (vcapPtr->IsRange.Value!=0)
                {
                    usageMin = (uint) (vcapPtr->Anonymous.Range.UsageMin & 0x00FF);
                }
                else
                {
                    usageMin = vcapPtr->Anonymous.NotRange.Usage;
                }
                usages[i] = ((uint)vcapPtr->UsagePage<<16)|usageMin;
                PInvoke.HidP_GetUsageValue(
                    HIDP_REPORT_TYPE.HidP_Input, pValueCaps->UsagePage, 0, 
                    (ushort)usageMin,out values[i],
                    (nint) (pPreparsedDataBuffer.ToPointer()),
                    new PSTR(report), raw->data.hid.dwSizeHid);
            }
        }

       
        
        AxisListeners?.Invoke(raw->header.hDevice,usages, values);
        // free buffers
        Marshal.FreeHGlobal(valueCapsPtr);
        Marshal.FreeHGlobal(usagesPtr);
        Marshal.FreeHGlobal(buttonCapsPtr);
        Marshal.FreeHGlobal(pPreparsedDataBuffer);
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