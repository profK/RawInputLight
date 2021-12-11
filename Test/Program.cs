// See https://aka.ms/new-console-template for more information

using System.Net.Mime;
using RawInputLight;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var wrapper = NativeAPI.OpenWindow();
        var rawInput = new RawInput(wrapper);
        rawInput.KeyStateChangeEvent += (arg1, state) =>
            Console.WriteLine("Keys: "+arg1 + " : " + state);
        rawInput.MouseStateChangeEvent += (i, i1, arg3, arg4) =>
            Console.WriteLine("Mouse: "+i + "," + i1 + " " + arg3 + " " + arg4.ToString("X"));
        rawInput.ButtonDownEvent += (usageBase, buttons) =>
        {
            Console.Write("Buttons: ");
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i])
                {
                    Console.Write((HIDDesktopUsages) (i + usageBase) + " ");
                }
            }

            Console.WriteLine();
        };
        rawInput.AxisEvent +=(usageBase, values) => 
        {
            Console.Write("Axes: ");
            for (int i = 0; i < values.Length; i++)
            {
                Console.Write((HIDDesktopUsages) (i + usageBase) + ":");
                Console.Write(values[i]+" ");
            }

            Console.WriteLine();
        };    
        NativeAPI.MessagePump(wrapper);
       
    }
}