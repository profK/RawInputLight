using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace RawInputLight;

public static class NativeAPI
{
    public static Action<ushort, KeyState> KeyListeners;
    public static Action<int, int, int, uint> MouseStateListeners; 
    public struct HID_DEV_ID
    {
        public ushort page;
        public ushort usage;

        public HID_DEV_ID(ushort p,ushort u)
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
                    (uint)rawDevices.Length, (uint) sizeof(RAWINPUTDEVICE)))
            {
                return true;
            }
            else
            {
                Console.WriteLine("Error registering raw device: "+GetLastError());
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

        } while (!done) ;

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

                if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEKEYBOARD)
                {
                    switch (raw->data.keyboard.Message)
                    {
                        case (int) WindowsMessages.WM_KEYDOWN:
                            KeyListeners?.Invoke(raw->data.keyboard.VKey, KeyState.KeyDown);
                            break;
                        case (int) WindowsMessages.WM_KEYUP:
                            KeyListeners?.Invoke(raw->data.keyboard.VKey, KeyState.KeyUp);
                            break;
                    }
                } 
                else if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEMOUSE)
                {
                    MouseStateListeners?.Invoke(raw->data.mouse.lLastX,
                        raw->data.mouse.lLastY, 0, raw->data.mouse.ulRawButtons);
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