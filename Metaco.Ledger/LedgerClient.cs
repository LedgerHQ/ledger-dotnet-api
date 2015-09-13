using HidLibrary;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.WinUsb;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Metaco.Ledger
{
    public class LedgerClient
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, int dwFlags);

        static LedgerClient()
        {
            System.Reflection.Assembly myass = System.Reflection.Assembly.GetExecutingAssembly();
            FileInfo fi = new FileInfo(myass.Location);
            string folder = IntPtr.Size == 8 ? "x64" : "x86";
            System.IntPtr moduleHandle = LoadLibraryEx(fi.Directory.FullName + "\\" + folder + "\\hidapi.dll", IntPtr.Zero, 0);
            if(moduleHandle == IntPtr.Zero)
            {
                if(Marshal.GetLastWin32Error() != 0x7E)
                    throw new Win32Exception();
                moduleHandle = LoadLibraryEx(Directory.GetCurrentDirectory() + "\\" + folder + "\\hidapi.dll", IntPtr.Zero, 0);
                if(moduleHandle == IntPtr.Zero)
                {
                    throw new Win32Exception();
                }
            }
        }

        private HidDevice _Device;

        private static int[] OK = new[] { BTChipConstants.SW_OK };


        internal LedgerClient(HidDevice device)
        {
            if(device == null)
                throw new ArgumentNullException("device");
            _Device = device;
        }

        public HidDevice Device
        {
            get
            {
                return _Device;
            }
        }

        BTChipTransport _Transport;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private BTChipTransport Transport
        {
            get
            {
                return _Transport = _Transport ?? new BTChipTransport(_Device);
            }
        }


        public static unsafe IEnumerable<LedgerClient> GetLedgers()
        {
            //UsbDeviceFinder finder = new UsbDeviceFinder(0x2581, 0x3b7c);
            //var resu = UsbDevice.OpenUsbDevice(finder);

            //uint nb = 0;
            //Putin.GetRawInputDeviceList(null, ref nb, (uint)Marshal.SizeOf(typeof(Putin.RAWINPUTDEVICELIST)));
            //var array = new Putin.RAWINPUTDEVICELIST[nb];
            //Putin.GetRawInputDeviceList(array, ref nb, (uint)Marshal.SizeOf(typeof(Putin.RAWINPUTDEVICELIST)));



            //array = array.Where(_ => _.Type == Putin.RawInputDeviceType.HID).ToArray();
            //var ledgers = array
            //    .Select(i =>
            //    {
            //        Metaco.Ledger.Putin.RID_DEVICE_INFO info = new Putin.RID_DEVICE_INFO();
            //        info.cbSize = Marshal.SizeOf(typeof(Metaco.Ledger.Putin.RID_DEVICE_INFO));
            //        uint size = 0;
            //        Putin.GetRawInputDeviceInfo(i.hDevice, 0x2000000bU, &info, ref size);
            //        Putin.GetRawInputDeviceInfo(i.hDevice, 0x2000000bU, &info, ref size);
            //        return new
            //        {
            //            info,
            //            i
            //        };
            //    })
            //    .Where(i => i.info.hid.dwProductId == 0x3b7c && i.info.hid.dwVendorId == 0x2581)
            //    .Select(i => new LedgerClient(new SafeFileHandle(i.i.hDevice, true)))
            //    .ToList();


            var ledgers = HidLibrary.HidDevices.Enumerate(0x2581, 0x3b7c)
                            .Select(i => new LedgerClient(i))
                            .ToList();
            return ledgers;
        }

        private static string[] ToStrings(byte[] mszReaders, int pcchReaders)
        {
            char nullchar = (char)0;
            int nullindex = -1;
            List<string> readersList = new List<string>();
            ASCIIEncoding ascii = new ASCIIEncoding();
            string currbuff = ascii.GetString(mszReaders);
            int len = pcchReaders;

            while(currbuff[0] != nullchar)
            {
                nullindex = currbuff.IndexOf(nullchar);   //get null end character
                string reader = currbuff.Substring(0, nullindex);
                readersList.Add(reader);
                len = len - (reader.Length + 1);
                currbuff = currbuff.Substring(nullindex + 1, len);
            }
            return readersList.ToArray();
        }

        private static AnonymousHandle EstablishContext()
        {
            int ctx = 0;
            int err = ModWinsCard.SCardEstablishContext(ModWinsCard.SCARD_SCOPE_USER, 0, 0, ref ctx);
            if(err != ModWinsCard.SCARD_S_SUCCESS)
                throw new LedgerException(err);
            return new AnonymousHandle(ctx, i => ModWinsCard.SCardReleaseContext(i.Handle));
        }



        public BTChipFirmware GetFirmwareVersion()
        {
            byte[] response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_FIRMWARE_VERSION, (byte)0x00, (byte)0x00, 0x00, OK);
            return new BTChipFirmware(response);
        }


        private byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
        {
            byte[] apdu = new byte[data.Length + 5];
            apdu[0] = cla;
            apdu[1] = ins;
            apdu[2] = p1;
            apdu[3] = p2;
            apdu[4] = (byte)(data.Length);
            Array.Copy(data, 0, apdu, 5, data.Length);
            return ExchangeCheck(apdu, acceptedSW);
        }
        private byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, int length, int[] acceptedSW)
        {
            byte[] apdu = new byte[]
            {
                cla,ins,p1,p2,(byte)length
            };
            return ExchangeCheck(apdu, acceptedSW);
        }

        private byte[] ExchangeCheck(byte[] apdu, int[] acceptedSW)
        {
            int lastSW;
            byte[] response = Exchange(apdu, out lastSW);
            if(acceptedSW == null)
            {
                return response;
            }
            foreach(int SW in acceptedSW)
            {
                if(lastSW == SW)
                {
                    return response;
                }
            }
            throw new BTChipException("Invalid status", lastSW);
        }

        private byte[] Exchange(byte[] apdu, out int sw)
        {
            byte[] response = Transport.Exchange(apdu);
            if(response.Length < 2)
            {
                throw new BTChipException("Truncated response");
            }
            sw = ((int)(response[response.Length - 2] & 0xff) << 8) |
                    (int)(response[response.Length - 1] & 0xff);
            byte[] result = new byte[response.Length - 2];
            Array.Copy(response, 0, result, 0, response.Length - 2);
            return result;
        }
    }

    [Flags]
    public enum FirmwareFeatures : byte
    {
        Compressed = 0x01,
        SecureElementUI = 0x02,
        ExternalUI = 0x04,
        NFC = 0x08,
        BLE = 0x10,
        TrustedEnvironmentExecution = 0x20
    }


    //https://ledgerhq.github.io/btchip-doc/bitcoin-technical.html#_get_firmware_version
    public class BTChipFirmware
    {
        public BTChipFirmware(int major, int minor, int patch, bool compressedKeys)
        {

        }

        public BTChipFirmware(byte[] bytes)
        {
            _Features = (FirmwareFeatures)(bytes[0] & ~0xC0);
            _Architecture = bytes[1];
            _Major = bytes[2];
            _Minor = bytes[3];
            _Patch = bytes[4];
            _LoaderMinor = bytes[5];
            _LoaderMajor = bytes[6];
        }

        private readonly FirmwareFeatures _Features;
        public FirmwareFeatures Features
        {
            get
            {
                return _Features;
            }
        }

        private readonly byte _Architecture;
        public byte Architecture
        {
            get
            {
                return _Architecture;
            }
        }

        private readonly byte _Major;
        public byte Major
        {
            get
            {
                return _Major;
            }
        }

        private readonly byte _Minor;
        public byte Minor
        {
            get
            {
                return _Minor;
            }
        }

        private readonly byte _Patch;
        public byte Patch
        {
            get
            {
                return _Patch;
            }
        }


        private readonly byte _LoaderMajor;
        public byte LoaderMajor
        {
            get
            {
                return _LoaderMajor;
            }
        }

        private readonly byte _LoaderMinor;
        public byte LoaderMinor
        {
            get
            {
                return _LoaderMinor;
            }
        }

        public string ToString()
        {
            return (Architecture != 0 ? "Ledger " : "") + string.Format("{0}.{1}.{2} (Loader : {3}.{4})", Major, Minor, Patch, LoaderMajor, LoaderMinor);
        }
    }
}
