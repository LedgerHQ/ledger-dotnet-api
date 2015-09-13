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
                _Transport = _Transport ?? new BTChipTransport(_Device);
                if(!_Device.IsConnected)
                {
                    throw new BTChipException("The device is not connected");
                }
                if(!_Device.IsOpen)
                {
                    throw new BTChipException("Error while opening the device");
                }
                return _Transport;
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





        private byte[] ToArray(byte[] bytes)
        {
            return new byte[] { (byte)bytes.Length }.Concat(bytes).ToArray();
        }


        private byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
        {
            int sw;
            var response = ExchangeApdu(cla, ins, p1, p2, data, out sw);
            CheckSW(acceptedSW, sw);
            return response;
        }

        private static void CheckSW(int[] acceptedSW, int sw)
        {
            if(!acceptedSW.Contains(sw))
            {
                throw new BTChipException(sw);
            }
        }

        private byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, byte[] data, out int sw)
        {
            sw = 0;
            byte[] apdu = new byte[data.Length + 5];
            apdu[0] = cla;
            apdu[1] = ins;
            apdu[2] = p1;
            apdu[3] = p2;
            apdu[4] = (byte)(data.Length);
            Array.Copy(data, 0, apdu, 5, data.Length);
            return Exchange(apdu, out sw);
        }
        private byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, int length, int[] acceptedSW)
        {
            byte[] apdu = new byte[]
            {
                cla,ins,p1,p2,(byte)length
            };
            return ExchangeApdu(apdu, acceptedSW);
        }

        private byte[] ExchangeApdu(byte[] apdu, int[] acceptedSW)
        {
            int sw;
            var resp = Exchange(apdu, out sw);
            CheckSW(acceptedSW, sw);
            return resp;
        }

        private byte[] Exchange(byte[] apdu, out int sw)
        {
            byte[] response = Transport.Exchange(apdu);
            if(response == null)
                throw new BTChipException("Error while transmission");
            if(response.Length < 2)
            {
                throw new BTChipException("Truncated response");
            }
            sw = ((int)(response[response.Length - 2] & 0xff) << 8) |
                    (int)(response[response.Length - 1] & 0xff);
            if(sw == 0x6faa)
                throw new BTChipException(sw);
            byte[] result = new byte[response.Length - 2];
            Array.Copy(response, 0, result, 0, response.Length - 2);
            return result;
        }


        public BTChipFirmware GetFirmwareVersion()
        {
            byte[] response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_FIRMWARE_VERSION, (byte)0x00, (byte)0x00, 0x00, OK);
            return new BTChipFirmware(response);
        }

        public bool VerifyPin(string pin, out int remaining)
        {
            int lastSW;
            remaining = 3;
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_VERIFY_PIN, 0, 0, Encoding.ASCII.GetBytes(pin), out lastSW);
            if(lastSW == BTChipConstants.SW_OK)
                return true;
            remaining = (lastSW & 0x0F);
            return false;
        }
        public int GetRemainingAttempts()
        {
            int lastSW;
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_VERIFY_PIN, 0x80, 0, new byte[] { 1 }, out lastSW);
            return (lastSW & 0x0F);
        }

        public bool VerifyPin(string pin)
        {
            int remain;
            return VerifyPin(pin, out remain);
        }

        public OperationMode GetOperationMode()
        {
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_OPERATION_MODE, 0, 0, 0, OK);
            return (OperationMode)response[0];
        }

        public SecondFactorMode GetSecondFactorMode()
        {
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_OPERATION_MODE, 1, 0, 0, OK);
            return (SecondFactorMode)response[0];
        }

        public void SetOperationMode(OperationMode value)
        {
            ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_SET_OPERATION_MODE, 0, 0, new[] { (byte)value }, OK);
        }

        public void RegularSetup(RegularSetup setup)
        {
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_SETUP, 0, 0, setup.ToBytes(), OK);

        }
    }

    public class RegularSetup
    {
        public RegularSetup()
        {
            AcceptedCoinVersions = 0;
            OperationMode = Ledger.OperationMode.Standard;
            DongleFeatures = Ledger.DongleFeatures.RFC6979;

        }
        public OperationMode OperationMode
        {
            get;
            set;
        }

        public DongleFeatures DongleFeatures
        {
            get;
            set;
        }

        public byte AcceptedCoinVersions
        {
            get;
            set;
        }

        public byte AcceptedCoinVersionsP2SH
        {
            get;
            set;
        }

        public UserPin UserPin
        {
            get;
            set;
        }

        public UserPin SecondaryUserPin
        {
            get;
            set;
        }

        public byte[] RestoredSeed
        {
            get;
            set;
        }
        public byte[] RestoredWrappingKey
        {
            get;
            set;
        }

        internal byte[] ToBytes()
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte((byte)OperationMode);
            ms.WriteByte((byte)DongleFeatures);
            ms.WriteByte(AcceptedCoinVersions);
            ms.WriteByte(AcceptedCoinVersionsP2SH);
            var bytes = UserPin.ToBytes();
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            bytes = SecondaryUserPin == null ? new UserPin().ToBytes() : SecondaryUserPin.ToBytes();
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            bytes = RestoredSeed ?? new byte[0];
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            bytes = RestoredWrappingKey ?? new byte[0];
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            return ms.ToArray();
        }
    }

    public class UserPin
    {
        public UserPin()
        {
            _Bytes = new byte[0];
        }
        public UserPin(string pin)
        {
            _Bytes = Encoding.ASCII.GetBytes(pin);
        }
        public UserPin(byte[] bytes)
        {
            _Bytes = bytes.ToArray();
        }
        byte[] _Bytes;
        public byte[] ToBytes()
        {
            return _Bytes.ToArray();
        }
    }

    [Flags]
    public enum DongleFeatures : byte
    {
        Uncompressed = 0x01,
        RFC6979 = 0x02,
        /// <summary>
        /// Authorize all signature hashtypes (otherwise only authorize SIGHASH_ALL)
        /// </summary>
        EnableAllSigHash = 0x04,
        /// <summary>
        ///  Skip second factor, allow relaxed inputs and arbitrary ouput scripts if consuming P2SH inputs in a transaction
        /// </summary>
        SkipSecondFactor = 0x08,
    }

    public enum SecondFactorMode
    {
        Keyboard = 0x11,
        SecurityCard = 0x12,
        SecurityCardAndSecureScreen = 0x13,
    }


    public enum OperationMode
    {
        Standard = 0x01,
        Relaxed = 0x02,
        Server = 0x04,
        Developer = 0x08
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
