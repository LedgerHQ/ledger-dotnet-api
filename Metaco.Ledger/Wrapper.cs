using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Metaco.Ledger.SetupApi.DeviceInstallation;

namespace Metaco.Ledger
{
    namespace SetupApi.DeviceInstallation
    {
        using System;
        using System.Collections.Generic;
        using System.ComponentModel;
        using System.Runtime.InteropServices;
        using Microsoft.Win32.SafeHandles;

        [Flags]
        public enum Digcf : uint
        {
            Default = 0x00000001, // only valid with DeviceInterface
            Present = 0x00000002,
            AllClasses = 0x00000004,
            Profile = 0x00000008,
            DeviceInterface = 0x00000010,
        }
        [StructLayout(LayoutKind.Sequential)]
        public sealed class DeviceInfoData
        {
            public DeviceInfoData()
            {
                cbSize = (UInt32)Marshal.SizeOf(this);
            }

            public Guid SetupClass
            {
                get
                {
                    return classGuid;
                }
            }
            public UInt32 DeviceInstance
            {
                get
                {
                    return devInst;
                }
            }

            private UInt32 cbSize;
            private Guid classGuid;
            private UInt32 devInst;
            private IntPtr reserved;
        }

        // Device interface data
        [StructLayout(LayoutKind.Sequential)]
        public sealed class DeviceInterfaceData
        {
            public DeviceInterfaceData()
            {
                cbSize = (UInt32)Marshal.SizeOf(this);
            }

            public Guid InterfaceClass
            {
                get
                {
                    return interfaceClassGuid;
                }
            }
            public UInt32 Flags
            {
                get
                {
                    return flags;
                }
            }

            private UInt32 cbSize;
            private Guid interfaceClassGuid;
            private UInt32 flags;
            private IntPtr reserved;
        }



        public sealed class DeviceDriverList : IDisposable
        {
            public DeviceDriverList(Digcf flags)
                : this(Guid.Empty, null, IntPtr.Zero, flags)
            {
            }
            public DeviceDriverList(Guid classGuid, string enumerator, IntPtr hwndParent, Digcf flags)
            {
                handle = SetupDiGetClassDevs(ref classGuid, enumerator, hwndParent, (uint)flags);
                if(handle.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            ~DeviceDriverList()
            {
                Dispose();
            }

            public void Dispose()
            {
                handle.Dispose();
                GC.SuppressFinalize(this);
            }

            public IEnumerable<DeviceInfoData> EnumDeviceInfo()
            {
                for(uint memberIndex = 0; ; memberIndex++)
                {
                    if(handle.IsClosed)
                        break;
                    var deviceInfoData = new DeviceInfoData();
                    if(!SetupDiEnumDeviceInfo(handle, memberIndex, deviceInfoData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if(error == ERROR_NO_MORE_ITEMS)
                            break;
                        throw new Win32Exception(error);
                    }
                    yield return deviceInfoData;
                }
            }

            public IEnumerable<DeviceInterfaceData> EnumDeviceInterfaces(DeviceInfoData deviceInfoData, Guid interfaceClassGuid)
            {
                for(uint memberIndex = 0; ; memberIndex++)
                {
                    if(handle.IsClosed)
                        break;
                    var deviceInterfaceData = new DeviceInterfaceData();
                    if(!SetupDiEnumDeviceInterfaces(handle, deviceInfoData, IntPtr.Zero, memberIndex, deviceInterfaceData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if(error == ERROR_NO_MORE_ITEMS)
                            break;
                        throw new Win32Exception(error);
                    }
                    yield return deviceInterfaceData;
                }
            }
            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SetupDiGetClassDevs(
                                                     ref Guid ClassGuid,
                                                     int Enumerator,
                                                     IntPtr hwndParent,
                                                     int Flags
                                                    );



            [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern Boolean SetupDiEnumDeviceInterfaces(
                                          IntPtr hDevInfo,
                                          ref Metaco.Ledger.USBClass.Win32Wrapper.SP_DEVINFO_DATA devInfo,
                                          ref Guid interfaceClassGuid,
                                          UInt32 memberIndex,
                                          ref Metaco.Ledger.USBClass.Win32Wrapper.SP_DEVICE_INTERFACE_DATA deviceInterfaceData
                                    );
            private SafeDeviceInfoListHandle handle;

            private const int ERROR_NO_MORE_ITEMS = 259;

            [DllImport("setupapi.dll", SetLastError = true)]
            extern static SafeDeviceInfoListHandle SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            extern static bool SetupDiEnumDeviceInterfaces(SafeDeviceInfoListHandle deviceInfoSet, DeviceInfoData deviceInfoData, IntPtr interfaceClassGuid, UInt32 memberIndex, DeviceInterfaceData deviceInterfaceData);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            extern static bool SetupDiEnumDeviceInfo(SafeDeviceInfoListHandle deviceInfoSet, uint memberIndex, DeviceInfoData deviceInfoData);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            extern static bool SetupDiDestroyDeviceInfoList(IntPtr handle);



            private sealed class SafeDeviceInfoListHandle : SafeHandleZeroOrMinusOneIsInvalid
            {
                public SafeDeviceInfoListHandle()
                    : base(true)
                {
                }
                public SafeDeviceInfoListHandle(IntPtr preexistingHandle, bool ownsHandle)
                    : base(ownsHandle)
                {
                    base.SetHandle(preexistingHandle);
                }

                protected override bool ReleaseHandle()
                {
                    return SetupDiDestroyDeviceInfoList(this.handle);
                }
            }
        }
    }

    class Putin
    {

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public RawInputDeviceType Type;
        }

        public enum RawInputDeviceType : uint
        {
            MOUSE = 0,
            KEYBOARD = 1,
            HID = 2
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetRawInputDeviceList
        (
            [In, Out] RAWINPUTDEVICELIST[] RawInputDeviceList,
            ref uint NumDevices,
            uint Size /* = (uint)Marshal.SizeOf(typeof(RawInputDeviceList)) */
        );

        [DllImport("user32.dll", SetLastError = true)]
        public static unsafe extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, RID_DEVICE_INFO* pData, ref uint pcbSize);


        [StructLayout(LayoutKind.Sequential)]
        public struct RID_DEVICE_INFO_HID
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwVendorId;
            [MarshalAs(UnmanagedType.U4)]
            public int dwProductId;
            [MarshalAs(UnmanagedType.U4)]
            public int dwVersionNumber;
            [MarshalAs(UnmanagedType.U2)]
            public ushort usUsagePage;
            [MarshalAs(UnmanagedType.U2)]
            public ushort usUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RID_DEVICE_INFO_KEYBOARD
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwType;
            [MarshalAs(UnmanagedType.U4)]
            public int dwSubType;
            [MarshalAs(UnmanagedType.U4)]
            public int dwKeyboardMode;
            [MarshalAs(UnmanagedType.U4)]
            public int dwNumberOfFunctionKeys;
            [MarshalAs(UnmanagedType.U4)]
            public int dwNumberOfIndicators;
            [MarshalAs(UnmanagedType.U4)]
            public int dwNumberOfKeysTotal;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RID_DEVICE_INFO_MOUSE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwId;
            [MarshalAs(UnmanagedType.U4)]
            public int dwNumberOfButtons;
            [MarshalAs(UnmanagedType.U4)]
            public int dwSampleRate;
            [MarshalAs(UnmanagedType.U4)]
            public int fHasHorizontalWheel;
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct RID_DEVICE_INFO
        {
            [FieldOffset(0)]
            public int cbSize;
            [FieldOffset(4)]
            public int dwType;
            [FieldOffset(8)]
            public RID_DEVICE_INFO_MOUSE mouse;
            [FieldOffset(8)]
            public RID_DEVICE_INFO_KEYBOARD keyboard;
            [FieldOffset(8)]
            public RID_DEVICE_INFO_HID hid;
        }

    }
}
