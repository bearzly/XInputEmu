/* Copyright (c) Benjamin Gwin 2016
 * This software is licensed under the terms of the GPLv3.
 * See https://www.gnu.org/licenses/gpl-3.0.en.html for details.
 */

using EasyHook;
using System;
using System.Diagnostics;
using System.Runtime.Remoting;
using XInputHook;

namespace XInputEmu
{

    class Program
    {
        static String ChannelName = null;

        static void Main(string[] args)
        {
            try
            {
                Config.Register("XInput hook", "XInputEmu.exe", "XInputHook.dll");
                RemoteHooking.IpcCreateServer<XInputHookInterface>(ref ChannelName, WellKnownObjectMode.SingleCall);
                int pid;
                if (args.Length == 1)
                {
                    pid = Int32.Parse(args[0]);
                }
                else
                {
                    Process[] processes = Process.GetProcessesByName("DARKSOULS");
                    pid = processes[0].Id;
                }
                RemoteHooking.Inject(pid, "XInputHook.dll", "XInputHook.dll", ChannelName);

                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("caught exception {0}", e.ToString());
            }

        }
    }
}
