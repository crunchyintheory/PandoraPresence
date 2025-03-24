using BorderlandsDiscordRP;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BorderlandsDiscordRP
{
    public static class Program
    {
        [DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        private const int StdOutputHandle = -11;
        private const int MyCodePage = 437;
        // ReSharper disable once ConvertToConstant.Local
        private static readonly bool ShowConsole = false; //Or false if you don't want to see the console*/

        public static void Main(string[] args)
        {
            if (Program.ShowConsole)
            {
                Program.AllocConsole();
                IntPtr stdHandle = Program.GetStdHandle(Program.StdOutputHandle);
                Microsoft.Win32.SafeHandles.SafeFileHandle safeFileHandle = new(stdHandle, true);
                FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                System.Text.Encoding encoding = System.Text.Encoding.GetEncoding(Program.MyCodePage);
                StreamWriter standardOutput = new StreamWriter(fileStream, encoding);
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);
            }

            Integration.Create();
        }
    }
}