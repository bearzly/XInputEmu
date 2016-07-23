/* Copyright (c) Benjamin Gwin 2016
 * This software is licensed under the terms of the GPLv3.
 * See https://www.gnu.org/licenses/gpl-3.0.en.html for details.
 */


using EasyHook;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace XInputHook
{
    public class XInputHookInterface : MarshalByRefObject
    {
        public void Installed(Int32 InClientPID)
        {
            Console.WriteLine("xinput hook installed in target {0}", InClientPID);
        }

        public void OnGetInputState(Int32 InUserIndex, Int32 ret, RawState state)
        {
            Console.WriteLine("XInputGetState called on index {0} ret {3} y axis {1} packet {2}", InUserIndex, state.Gamepad.sThumbLY, state.dwPacketNumber, ret);
        }

        public void ReportException(Exception e)
        {
            Console.WriteLine("Exception from lib: {0}", e.ToString());
        }

        public void Notify(string s)
        {
            Console.WriteLine(s);
        }

        public void Ping()
        {

        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RawState
    {
        public RawState(RawState state)
        {
            dwPacketNumber = state.dwPacketNumber;
            Gamepad.wButtons = state.Gamepad.wButtons;
            Gamepad.bLeftTrigger = state.Gamepad.bLeftTrigger;
            Gamepad.bRightTrigger = state.Gamepad.bRightTrigger;
            Gamepad.sThumbLX = state.Gamepad.sThumbLX;
            Gamepad.sThumbLY = state.Gamepad.sThumbLY;
            Gamepad.sThumbRX = state.Gamepad.sThumbRX;
            Gamepad.sThumbRY = state.Gamepad.sThumbRY;
        }
        public uint dwPacketNumber;
        public GamePad Gamepad;

        public override string ToString()
        {
            return String.Format("packet {0} state {1}", dwPacketNumber, Gamepad);
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct GamePad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;

            public override string ToString()
            {
                return String.Format("buttons {0} LT {1} RT {2} LX {3} LY {4} RX {5} RY {6}", 
                    wButtons, bLeftTrigger, bRightTrigger, sThumbLX, sThumbLY, sThumbRX, sThumbRY);
            }
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct XCapabilities
	{
        public byte Type;
        public byte SubType;
        public short Flags;
        public RawState.GamePad Gamepad;
        public XVibration Vibration;

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct XVibration
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
            public override string ToString()
            {
                return String.Format("vibleft {0} vibright {1}", wLeftMotorSpeed, wRightMotorSpeed);
            }
        }

        public override string ToString()
        {
            return String.Format("type {0} subtype {1} flags {2} gamepad {3} vibration {4}", Type, SubType, Flags, Gamepad, Vibration);
        }
	}

    public class XInputHook : EasyHook.IEntryPoint
    {
        XInputHookInterface Interface;
        LocalHook Hook;
        LocalHook Hook2;
        Mutex stateMtx;
        RawState state;

        public XInputHook(RemoteHooking.IContext InContext, String InChannelName)
        {
            stateMtx = new Mutex();
            Interface = RemoteHooking.IpcConnectClient<XInputHookInterface>(InChannelName);
        }

        public void Run(RemoteHooking.IContext InContext, String InChannelName)
        {
            try
            {
                string dll = "xinput1_3.dll";
                Hook = LocalHook.Create(LocalHook.GetProcAddress(dll, "XInputGetState"),
                                              new DXInputGetState(XInputGetState_Hooked), this);
                
                Hook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
                Hook2 = LocalHook.Create(LocalHook.GetProcAddress(dll, "XInputGetCapabilities"),
                                              new DXInputGetCapabilities(XInputGetCapabilities_Hooked), this);
                Hook2.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
            }
            catch (Exception e)
            {
                Interface.ReportException(e);
                return;
            }
            Interface.Installed(RemoteHooking.GetCurrentProcessId());

            const int port = 13000;
            UdpClient listener = new UdpClient(port);
            listener.Client.ReceiveTimeout = 500;
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
            Interface.Notify("setup udp socket on port " + port);

            try
            {
                while (true)
                {
                    try
                    {
                        byte[] bytes = listener.Receive(ref ep);
                        string s = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
                        try
                        {
                            var parts = s.Split(' ');
                            ushort wButtons = ushort.Parse(parts[0]);
                            byte bLeftTrigger = byte.Parse(parts[1]);
                            byte bRightTrigger = byte.Parse(parts[2]);
                            short sThumbLX = short.Parse(parts[3]);
                            short sThumbLY = short.Parse(parts[4]);
                            short sThumbRX = short.Parse(parts[5]);
                            short sThumbRY = short.Parse(parts[6]);
                            stateMtx.WaitOne();
                            state.dwPacketNumber++;
                            state.Gamepad.wButtons = wButtons;
                            state.Gamepad.bLeftTrigger = bLeftTrigger;
                            state.Gamepad.bRightTrigger = bRightTrigger;
                            state.Gamepad.sThumbLX = sThumbLX;
                            state.Gamepad.sThumbLY = sThumbLY;
                            state.Gamepad.sThumbRX = sThumbRX;
                            state.Gamepad.sThumbRY = sThumbRY;
                            stateMtx.ReleaseMutex();
                        }
                        catch (Exception e)
                        {
                            Interface.Notify("got bad data: " + s + " " + e.Message);
                        }
                    }
                    catch (SocketException e)
                    {

                    }
                    Interface.Ping();
                }
            }
            catch (Exception e)
            {

            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate Int32 DXInputGetState(Int32 dwUserIndex, out RawState state);

        [DllImport("xinput1_3.dll", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        static extern Int32 XInputGetState(Int32 dwUserIndex, out RawState state);

        static Int32 XInputGetState_Hooked(Int32 dwUserIndex, out RawState state)
        {
            Int32 ret = 0;
            try
            {
                XInputHook This = (XInputHook)HookRuntimeInfo.Callback;
                ret = XInputGetState(dwUserIndex, out state);
                //This.Interface.Notify(String.Format("Called XInputGetState index {0} return {1} state {2}", dwUserIndex, ret, state));
                This.stateMtx.WaitOne();
                state.dwPacketNumber = This.state.dwPacketNumber;
                state.Gamepad.wButtons = This.state.Gamepad.wButtons;
                state.Gamepad.bLeftTrigger = This.state.Gamepad.bLeftTrigger;
                state.Gamepad.bRightTrigger = This.state.Gamepad.bRightTrigger;
                state.Gamepad.sThumbLX = This.state.Gamepad.sThumbLX;
                state.Gamepad.sThumbLY = This.state.Gamepad.sThumbLY;
                state.Gamepad.sThumbRX = This.state.Gamepad.sThumbRX;
                state.Gamepad.sThumbRY = This.state.Gamepad.sThumbRY;
                This.stateMtx.ReleaseMutex();
            }
            catch
            {
                state = new RawState();
            }
            return ret;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate Int32 DXInputGetCapabilities(Int32 dwUserIndex, Int32 dwFlags, out SharpDX.XInput.Capabilities caps);

        [DllImport("xinput1_3.dll", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        static extern Int32 XInputGetCapabilities(Int32 dwUserIndex, Int32 dwFlags, out SharpDX.XInput.Capabilities caps);

        static Int32 XInputGetCapabilities_Hooked(Int32 dwUserIndex, Int32 dwFlags, out SharpDX.XInput.Capabilities caps)
        {
            Int32 ret = 0;
            try
            {
                XInputHook This = (XInputHook)HookRuntimeInfo.Callback;
                ret = XInputGetCapabilities(dwUserIndex, dwFlags, out caps);
                //This.Interface.Notify(String.Format("Called XInputGetCapabilities index {0} flags {1} ret {2} caps {3}", dwUserIndex, dwFlags, caps));
            }
            catch { caps = new SharpDX.XInput.Capabilities(); }
            return ret;
        }
    }
}
