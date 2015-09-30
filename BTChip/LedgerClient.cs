using HidLibrary;
using Microsoft.Win32.SafeHandles;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
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
            var ledgers = HidLibrary.HidDevices.Enumerate(0x2581, 0x3b7c)
                            .Select(i => new LedgerClient(i))
                            .ToList();
            return ledgers;
        }

        private byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
        {
            int sw;
            var response = ExchangeApdu(cla, ins, p1, p2, data, out sw);
            CheckSW(acceptedSW, sw);
            return response;
        }

        private byte[] ExchangeApduSplit(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
        {
            int offset = 0;
            byte[] result = null;
            while(offset < data.Length)
            {
                int blockLength = ((data.Length - offset) > 255 ? 255 : data.Length - offset);
                byte[] apdu = new byte[blockLength + 5];
                apdu[0] = cla;
                apdu[1] = ins;
                apdu[2] = p1;
                apdu[3] = p2;
                apdu[4] = (byte)(blockLength);
                Array.Copy(data, offset, apdu, 5, blockLength);
                result = ExchangeApdu(apdu, acceptedSW);
                offset += blockLength;
            }
            return result;
        }

        private byte[] ExchangeApduSplit2(byte cla, byte ins, byte p1, byte p2, byte[] data, byte[] data2, int[] acceptedSW)
        {
            int offset = 0;
            byte[] result = null;
            int maxBlockSize = 255 - data2.Length;
            while(offset < data.Length)
            {
                int blockLength = ((data.Length - offset) > maxBlockSize ? maxBlockSize : data.Length - offset);
                var lastBlock = ((offset + blockLength) == data.Length);
                byte[] apdu = new byte[blockLength + 5 + (lastBlock ? data2.Length : 0)];
                apdu[0] = cla;
                apdu[1] = ins;
                apdu[2] = p1;
                apdu[3] = p2;
                apdu[4] = (byte)(blockLength + (lastBlock ? data2.Length : 0));
                Array.Copy(data, offset, apdu, 5, blockLength);
                if(lastBlock)
                {
                    Array.Copy(data2, 0, apdu, 5 + blockLength, data2.Length);
                }
                result = ExchangeApdu(apdu, acceptedSW);
                offset += blockLength;
            }
            return result;
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

        public GetWalletPubKeyResponse GetWalletPubKey(KeyPath keyPath)
        {
            Guard.AssertKeyPath(keyPath);
            byte[] bytes = Serializer.Serialize(keyPath);
            byte[] response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_WALLET_PUBLIC_KEY, (byte)0x00, (byte)0x00, bytes, OK);
            return new GetWalletPubKeyResponse(response);
        }

        public bool VerifyPin(string pin, out int remaining)
        {
            return VerifyPin(new UserPin(pin), out remaining);
        }
        public bool VerifyPin(UserPin pin, out int remaining)
        {
            int lastSW;
            remaining = 3;
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_VERIFY_PIN, 0, 0, pin.ToBytes(), out lastSW);
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
        public bool VerifyPin(UserPin pin)
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

        public SetupResponse RegularSetup(RegularSetup setup)
        {
            var response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_SETUP, 0, 0, setup.ToBytes(), OK);
            return new SetupResponse(response);
        }

        public TrustedInput GetTrustedInput(IndexedTxOut txout)
        {
            return GetTrustedInput(txout.Transaction, (int)txout.N);
        }
        public TrustedInput GetTrustedInput(Transaction transaction, int outputIndex)
        {
            if(outputIndex >= transaction.Outputs.Count)
                throw new ArgumentOutOfRangeException("outputIndex is bigger than the number of outputs in the transaction", "outputIndex");
            MemoryStream data = new MemoryStream();
            // Header
            BufferUtils.WriteUint32BE(data, outputIndex);
            BufferUtils.WriteBuffer(data, transaction.Version);
            VarintUtils.write(data, transaction.Inputs.Count);
            ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x00, (byte)0x00, data.ToArray(), OK);
            // Each input
            foreach(var input in transaction.Inputs)
            {
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, input.PrevOut);
                VarintUtils.write(data, input.ScriptSig.Length);
                ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, input.ScriptSig.ToBytes());
                ExchangeApduSplit2(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), Utils.ToBytes(input.Sequence, true), OK);
            }
            // Number of outputs
            data = new MemoryStream();
            VarintUtils.write(data, transaction.Outputs.Count);
            ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
            // Each output
            foreach(var output in transaction.Outputs)
            {
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, Utils.ToBytes((ulong)output.Value.Satoshi, true));
                VarintUtils.write(data, output.ScriptPubKey.Length);
                ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, output.ScriptPubKey.ToBytes());
                ExchangeApduSplit(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
            }
            // Locktime
            byte[] response = ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, transaction.LockTime.ToBytes(), OK);
            return new TrustedInput(response);
        }

        //public void startUntrustedTransaction(bool newTransaction, long inputIndex, BTChipInput[] usedInputList, byte[] redeemScript)
        //{
        //    // Start building a fake transaction with the passed inputs
        //    MemoryStream data = new MemoryStream();
        //    BufferUtils.WriteBuffer(data, BitcoinTransaction.DEFAULT_VERSION);
        //    VarintUtils.write(data, usedInputList.length);
        //    ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_HASH_INPUT_START, (byte)0x00, (newTransaction ? (byte)0x00 : (byte)0x80), data.toByteArray(), OK);
        //    // Loop for each input
        //    long currentIndex = 0;
        //    foreach(BTChipInput input in usedInputList)
        //    {
        //        byte[] script = (currentIndex == inputIndex ? redeemScript : new byte[0]);
        //        data = new MemoryStream();
        //        data.write(input.Trusted ? (byte)0x01 : (byte)0x00);
        //        if(input.Trusted)
        //        {
        //            // untrusted inputs have constant length
        //            data.write(input.getValue().length);
        //        }
        //        BufferUtils.WriteBuffer(data, input.getValue());
        //        VarintUtils.write(data, script.length);
        //        ExchangeApdu(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_HASH_INPUT_START, (byte)0x80, (byte)0x00, data.toByteArray(), OK);
        //        data = new MemoryStream();
        //        BufferUtils.WriteBuffer(data, script);
        //        BufferUtils.WriteBuffer(data, BitcoinTransaction.DEFAULT_SEQUENCE);
        //        ExchangeApduSplit(BTChipConstants.BTCHIP_CLA, BTChipConstants.BTCHIP_INS_HASH_INPUT_START, (byte)0x80, (byte)0x00, data.toByteArray(), OK);
        //        currentIndex++;
        //    }
        //}
    }





    public class RegularSetup
    {
        public RegularSetup()
        {
            OperationMode = OperationMode.Standard;
            DongleFeatures = DongleFeatures.RFC6979;
            AcceptedP2PKHVersion = 0;
            AcceptedP2SHVersion = 0x05;
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

        public byte AcceptedP2PKHVersion
        {
            get;
            set;
        }

        public byte AcceptedP2SHVersion
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
        public Ledger3DESKey RestoredWrappingKey
        {
            get;
            set;
        }

        internal byte[] ToBytes()
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte((byte)OperationMode);
            ms.WriteByte((byte)DongleFeatures);
            ms.WriteByte(AcceptedP2PKHVersion);
            ms.WriteByte(AcceptedP2SHVersion);
            var bytes = UserPin.ToBytes();
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            bytes = SecondaryUserPin == null ? new UserPin().ToBytes() : SecondaryUserPin.ToBytes();
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);

            bytes = RestoredSeed ?? new byte[0];
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);

            bytes = RestoredWrappingKey == null ? new byte[0] : RestoredWrappingKey.ToBytes();
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
