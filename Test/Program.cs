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
                    Console.WriteLine((HIDDesktopUsages) (i + usageBase) + " ");
                }
            }

            Console.WriteLine();
        };
            
        NativeAPI.MessagePump(wrapper);
       
    }
}