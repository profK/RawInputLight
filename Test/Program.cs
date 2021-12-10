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
            Console.WriteLine(arg1 + " : " + state);
        rawInput.MouseStateChangeEvent += (i, i1, arg3, arg4) =>
            Console.WriteLine(i + "," + i1 + " " + arg3 + " " + arg4.ToString("X"));
        NativeAPI.MessagePump(wrapper);
       
    }
}