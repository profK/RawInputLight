using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace RawInputLight;

public static class NativeAPI
{
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

    public static void RunApplication(HWND_WRAPPER hwndWrapper)
    {
        new Thread(MessagePump).Start(hwndWrapper);
    }

    [STAThread]
    private static async void MessagePump(object hwndWrapperObj)
    {
        HWND_WRAPPER hwndWrapper = (HWND_WRAPPER) hwndWrapperObj; 
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

            fixed (char* p2 = "RAWINPUTWINDOW")
            {
                var windownameStr = new PCWSTR(p2);
                var hwnd = PInvoke.CreateWindowEx(0, classnameStr, windownameStr,
                    WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
                    0, 0, 800, 600, new HWND(0), new HMENU(IntPtr.Zero),
                    windowClass.hInstance, null);
               
                if (hwnd.Value == IntPtr.Zero)
                    throw new Exception("Opening window failed: " +
                                        GetLastError());
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_NORMAL);
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
            case 0x00F: //WM_INPUT: dsfs
            {
                uint dwSize;

                PInvoke.GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                    IntPtr.Zero.ToPointer(), &dwSize, (uint) sizeof(RAWINPUTHEADER));

                if (dwSize == 0) return new LRESULT(0);

                var lpb = Marshal.AllocHGlobal((int) dwSize);
                if (PInvoke.GetRawInputData(new HRAWINPUT(lParam), RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT,
                        lpb.ToPointer(), &dwSize, (uint) sizeof(RAWINPUTHEADER)) != dwSize)
                    Console.WriteLine("GetRawInputData does not return correct size !\n");

                var raw = (RAWINPUT*) lpb;

                if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEKEYBOARD)
                    Console.WriteLine(" Kbd: make={0} Flags:{1} Reserved:{2} ExtraInformation:{3}, msg={4} VK={5}",
                        raw->data.keyboard.MakeCode, raw->data.keyboard.Flags, raw->data.keyboard.Reserved,
                        raw->data.keyboard.ExtraInformation, raw->data.keyboard.Message, raw->data.keyboard.VKey);
                else if (raw->header.dwType == (int) RAW_INPUT_TYPE.RIM_TYPEMOUSE)
                    Console.WriteLine("Mouse: usFlags={0} ulButtons={1} usButtonFlags={2} usButtonData={3} " +
                                      "ulRawButtons={4} lLastX={5} lLastY={6} ulExtraInformation={7}",
                        raw->data.mouse.usFlags, 0, 0, 0, raw->data.mouse.ulRawButtons, raw->data.mouse.lLastX,
                        raw->data.mouse.lLastY, raw->data.mouse.ulExtraInformation);
                Marshal.FreeHGlobal(lpb);

                return new LRESULT(0);
            }
            default:
                return new LRESULT(DefWindowProc(hWnd, uMsg, wParam, lParam));
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