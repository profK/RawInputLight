// See https://aka.ms/new-console-template for more information

using System.Net.Mime;
using RawInputLight;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        
        NativeAPI.RefreshDeviceInfo();
        foreach (var devInfo in NativeAPI.GetDevices())
        {
            Console.WriteLine(devInfo.Names.Product);
        }
        
        var wrapper = NativeAPI.OpenWindow();
        var rawInput = new RawInput(wrapper);
        rawInput.KeyStateChangeEvent += (devID, arg1, state) =>
        {
            Console.WriteLine("-------");
            Console.WriteLine(NativeAPI.GetDeviceInfo(devID).Value.Names.Product);
            Console.WriteLine("Keys: " + arg1 + " : " + state);
            Console.WriteLine("-------");
        };

        rawInput.MouseStateChangeEvent += (devID, i, i1, arg3, arg4) =>
        {
            Console.WriteLine("-------");
            Console.WriteLine(NativeAPI.GetDeviceInfo(devID).Value.Names.Product);
            Console.WriteLine("Mouse: " + i + "," + i1 + " " + arg3 + " " + arg4.ToString("X"));
            Console.WriteLine("-------");
        };
        rawInput.ButtonDownEvent += (devID,usageBase, buttons) =>
        {
            Console.WriteLine("-------");
            Console.WriteLine(NativeAPI.GetDeviceInfo(devID).Value.Names.Product);
            Console.Write("Buttons: ");
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i])
                {
                    Console.Write((HIDDesktopUsages) (i + usageBase) + " ");
                }
            }

            Console.WriteLine();
            Console.WriteLine("-------");
        };
        rawInput.AxisEvent +=(devID,usageBase, values) => 
        {
            Console.WriteLine("-------");
            Console.WriteLine(NativeAPI.GetDeviceInfo(devID).Value.Names.Product);
            Console.Write("Axes: ");
            for (int i = 0; i < values.Length; i++)
            {
                Console.Write((HIDDesktopUsages) (usageBase[i]) + ":");
                Console.Write(values[i]+" ");
            }

            Console.WriteLine();
            Console.WriteLine("-------");
        };    
        NativeAPI.MessagePump(wrapper);
       
    }
}