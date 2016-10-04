using HidLibrary;
using Microsoft.Win32.SafeHandles;
using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
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

        private static int[] OK = new[] { LedgerWalletConstants.SW_OK };

        object GetLock()
        {
            return GetLock(_Device.DevicePath);
        }

        static Dictionary<string, object> _Locks = new Dictionary<string, object>();
        static object GetLock(string key)
        {
            lock(_Locks)
            {
                object l;
                if(!_Locks.TryGetValue(key, out l))
                {
                    l = new object();
                    _Locks.Add(key, l);
                }
                return l;
            }
        }

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

        LedgerWalletTransport _Transport;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private LedgerWalletTransport Transport
        {
            get
            {
                _Transport = _Transport ?? new LedgerWalletTransport(_Device);
                if(!_Device.IsConnected)
                {
                    throw new LedgerWalletException("The device is not connected");
                }
                if(!_Device.IsOpen)
                {
                    throw new LedgerWalletException("Error while opening the device");
                }
                return _Transport;
            }
        }


        //https://github.com/LedgerHQ/ledger-wallet-chrome/blob/59f52dcedc031871d17cc69eb531bc6b4cf89a6b/app/libs/btchip/btchip-js-api/chromeApp/chromeDevice.js
        //https://github.com/LedgerHQ/ledger-wallet-chrome/blob/59f52dcedc031871d17cc69eb531bc6b4cf89a6b/app/src/dongle/manager.coffee
        public static unsafe IEnumerable<LedgerClient> GetLedgers()
        {
            var ledgers = HidLibrary.HidDevices.Enumerate(0x2c97)
                            .Concat(HidLibrary.HidDevices.Enumerate(0x2581, 0x3b7c))
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
                throw new LedgerWalletException(sw);
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
                throw new LedgerWalletException("Error while transmission");
            if(response.Length < 2)
            {
                throw new LedgerWalletException("Truncated response");
            }
            sw = ((int)(response[response.Length - 2] & 0xff) << 8) |
                    (int)(response[response.Length - 1] & 0xff);
            if(sw == 0x6faa)
                throw new LedgerWalletException(sw);
            byte[] result = new byte[response.Length - 2];
            Array.Copy(response, 0, result, 0, response.Length - 2);
            return result;
        }


        public LedgerWalletFirmware GetFirmwareVersion()
        {
            lock(GetLock())
            {
                byte[] response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_FIRMWARE_VERSION, (byte)0x00, (byte)0x00, 0x00, OK);
                return new LedgerWalletFirmware(response);
            }
        }

        public GetWalletPubKeyResponse GetWalletPubKey(KeyPath keyPath)
        {
            lock(GetLock())
            {
                Guard.AssertKeyPath(keyPath);
                byte[] bytes = Serializer.Serialize(keyPath);
                //bytes[0] = 10;
                byte[] response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_WALLET_PUBLIC_KEY, (byte)0x00, (byte)0x00, bytes, OK);
                return new GetWalletPubKeyResponse(response);
            }
        }

        public bool VerifyPin(string pin, out int remaining)
        {
            return VerifyPin(new UserPin(pin), out remaining);
        }
        public bool VerifyPin(UserPin pin, out int remaining)
        {
            lock(GetLock())
            {
                int lastSW;
                remaining = 3;
                var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_VERIFY_PIN, 0, 0, pin.ToBytes(), out lastSW);
                if(lastSW == LedgerWalletConstants.SW_OK)
                    return true;
                if(lastSW == LedgerWalletConstants.SW_INS_NOT_SUPPORTED)
                    throw new LedgerWalletException(lastSW);
                remaining = (lastSW & 0x0F);
                return false;
            }
        }
        public int GetRemainingAttempts()
        {
            lock(GetLock())
            {
                int lastSW;
                var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_VERIFY_PIN, 0x80, 0, new byte[] { 1 }, out lastSW);
                return (lastSW & 0x0F);
            }
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
            lock(GetLock())
            {
                var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_OPERATION_MODE, 0, 0, 0, OK);
                return (OperationMode)response[0];
            }
        }

        public SecondFactorMode GetSecondFactorMode()
        {
            lock(GetLock())
            {
                var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_OPERATION_MODE, 1, 0, 0, OK);
                return (SecondFactorMode)response[0];
            }
        }

        public void SetOperationMode(OperationMode value)
        {
            lock(GetLock())
            {
                ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_SET_OPERATION_MODE, 0, 0, new[] { (byte)value }, OK);
            }
        }

        public SetupResponse RegularSetup(RegularSetup setup)
        {
            lock(GetLock())
            {
                var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_SETUP, 0, 0, setup.ToBytes(), OK);
                return new SetupResponse(response);
            }
        }

        public TrustedInput GetTrustedInput(IndexedTxOut txout)
        {
            return GetTrustedInput(txout.Transaction, (int)txout.N);
        }
        public TrustedInput GetTrustedInput(Transaction transaction, int outputIndex)
        {
            lock(GetLock())
            {
                if(outputIndex >= transaction.Outputs.Count)
                    throw new ArgumentOutOfRangeException("outputIndex is bigger than the number of outputs in the transaction", "outputIndex");
                MemoryStream data = new MemoryStream();
                // Header
                BufferUtils.WriteUint32BE(data, outputIndex);
                BufferUtils.WriteBuffer(data, transaction.Version);
                VarintUtils.write(data, transaction.Inputs.Count);
                ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x00, (byte)0x00, data.ToArray(), OK);
                // Each input
                foreach(var input in transaction.Inputs)
                {
                    data = new MemoryStream();
                    BufferUtils.WriteBuffer(data, input.PrevOut);
                    VarintUtils.write(data, input.ScriptSig.Length);
                    ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                    data = new MemoryStream();
                    BufferUtils.WriteBuffer(data, input.ScriptSig.ToBytes());
                    ExchangeApduSplit2(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), Utils.ToBytes(input.Sequence, true), OK);
                }
                // Number of outputs
                data = new MemoryStream();
                VarintUtils.write(data, transaction.Outputs.Count);
                ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                // Each output
                foreach(var output in transaction.Outputs)
                {
                    data = new MemoryStream();
                    BufferUtils.WriteBuffer(data, Utils.ToBytes((ulong)output.Value.Satoshi, true));
                    VarintUtils.write(data, output.ScriptPubKey.Length);
                    ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                    data = new MemoryStream();
                    BufferUtils.WriteBuffer(data, output.ScriptPubKey.ToBytes());
                    ExchangeApduSplit(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                }
                // Locktime
                byte[] response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, transaction.LockTime.ToBytes(), OK);
                return new TrustedInput(response);
            }
        }

        public void UntrustedHashTransactionInputStart(bool newTransaction, Transaction tx, int index, TrustedInput[] trustedInputs)
        {
            UntrustedHashTransactionInputStart(newTransaction, tx.Inputs.AsIndexedInputs().Skip(index).First(), trustedInputs);
        }
        public void UntrustedHashTransactionInputStart(bool newTransaction, IndexedTxIn txIn, TrustedInput[] trustedInputs)
        {
            lock(GetLock())
            {
                trustedInputs = trustedInputs ?? new TrustedInput[0];
                // Start building a fake transaction with the passed inputs
                MemoryStream data = new MemoryStream();
                BufferUtils.WriteBuffer(data, txIn.Transaction.Version);
                VarintUtils.write(data, txIn.Transaction.Inputs.Count);
                ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, (byte)0x00, (newTransaction ? (byte)0x00 : (byte)0x80), data.ToArray(), OK);
                // Loop for each input
                long currentIndex = 0;
                foreach(var input in txIn.Transaction.Inputs)
                {
                    var trustedInput = trustedInputs.FirstOrDefault(i => i.OutPoint == input.PrevOut);
                    byte[] script = (currentIndex == txIn.Index ? txIn.TxIn.ScriptSig.ToBytes() : new byte[0]);
                    data = new MemoryStream();
                    if(trustedInput != null)
                    {
                        data.WriteByte(0x01);
                        var b = trustedInput.ToBytes();
                        // untrusted inputs have constant length
                        data.WriteByte((byte)b.Length);
                        BufferUtils.WriteBuffer(data, b);
                    }
                    else
                    {
                        data.WriteByte(0x00);
                        BufferUtils.WriteBuffer(data, input.PrevOut);
                    }
                    VarintUtils.write(data, script.Length);
                    ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                    data = new MemoryStream();
                    BufferUtils.WriteBuffer(data, script);
                    BufferUtils.WriteBuffer(data, input.Sequence);
                    ExchangeApduSplit(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, (byte)0x80, (byte)0x00, data.ToArray(), OK);
                    currentIndex++;
                }
            }
        }

        public byte[] UntrustedHashTransactionInputFinalizeFull(IEnumerable<TxOut> outputs)
        {
            lock(GetLock())
            {
                byte[] result = null;
                int offset = 0;
                byte[] response = null;
                var ms = new MemoryStream();
                BitcoinStream bs = new BitcoinStream(ms, true);
                var list = outputs.ToList();
                bs.ReadWrite<List<TxOut>, TxOut>(ref list);
                var data = ms.ToArray();

                while(offset < data.Length)
                {
                    int blockLength = ((data.Length - offset) > 255 ? 255 : data.Length - offset);
                    byte[] apdu = new byte[blockLength + 5];
                    apdu[0] = LedgerWalletConstants.LedgerWallet_CLA;
                    apdu[1] = LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_FINALIZE_FULL;
                    apdu[2] = ((offset + blockLength) == data.Length ? (byte)0x80 : (byte)0x00);
                    apdu[3] = (byte)0x00;
                    apdu[4] = (byte)(blockLength);
                    Array.Copy(data, offset, apdu, 5, blockLength);
                    response = ExchangeApdu(apdu, OK);
                    offset += blockLength;
                }
                result = response; //convertResponseToOutput(response);
                if(result == null)
                {
                    throw new LedgerWalletException("Unsupported user confirmation method");
                }
                return result;
            }
        }


        public Transaction SignTransaction(KeyPath keyPath, ICoin[] signedCoins, Transaction[] parents, Transaction transaction)
        {
            lock(GetLock())
            {
                var pubkey = GetWalletPubKey(keyPath).UncompressedPublicKey.Compress();
                var parentsById = parents.ToDictionary(p => p.GetHash());
                var coinsByPrevout = signedCoins.ToDictionary(c => c.Outpoint);

                List<TrustedInput> trustedInputs = new List<TrustedInput>();
                foreach(var input in transaction.Inputs)
                {
                    Transaction parent;
                    parentsById.TryGetValue(input.PrevOut.Hash, out parent);
                    if(parent == null)
                        throw new KeyNotFoundException("Parent transaction " + input.PrevOut.Hash + " not found");
                    trustedInputs.Add(GetTrustedInput(parent, (int)input.PrevOut.N));
                }

                var inputs = trustedInputs.ToArray();

                transaction = transaction.Clone();

                foreach(var input in transaction.Inputs)
                {
                    ICoin previousCoin = null;
                    coinsByPrevout.TryGetValue(input.PrevOut, out previousCoin);

                    if(previousCoin != null)
                        input.ScriptSig = previousCoin.GetScriptCode();
                }

                bool newTransaction = true;
                foreach(var input in transaction.Inputs.AsIndexedInputs())
                {
                    ICoin coin = null;
                    if(!coinsByPrevout.TryGetValue(input.PrevOut, out coin))
                        continue;

                    UntrustedHashTransactionInputStart(newTransaction, input, inputs);
                    newTransaction = false;

                    UntrustedHashTransactionInputFinalizeFull(transaction.Outputs);

                    var sig = UntrustedHashSign(keyPath, null, transaction.LockTime, SigHash.All);
                    input.ScriptSig = PayToPubkeyHashTemplate.Instance.GenerateScriptSig(sig, pubkey);
                    ScriptError error;
                    if(!Script.VerifyScript(coin.TxOut.ScriptPubKey, transaction, (int)input.Index, Money.Zero, out error))
                        return null;
                }

                return transaction;
            }
        }

        public TransactionSignature UntrustedHashSign(KeyPath keyPath, UserPin pin, LockTime lockTime, SigHash sigHashType)
        {
            lock(GetLock())
            {
                MemoryStream data = new MemoryStream();
                byte[] path = Serializer.Serialize(keyPath);
                BufferUtils.WriteBuffer(data, path);

                var pinBytes = pin == null ? new byte[0] : pin.ToBytes();
                data.WriteByte((byte)pinBytes.Length);
                BufferUtils.WriteBuffer(data, pinBytes);
                BufferUtils.WriteUint32BE(data, (uint)lockTime);
                data.WriteByte((byte)sigHashType);
                byte[] response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_SIGN, (byte)0x00, (byte)0x00, data.ToArray(), OK);
                response[0] = (byte)0x30;
                return new TransactionSignature(response);
            }
        }
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

        public int Length
        {
            get
            {
                return _Bytes.Length;
            }
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


    //https://ledgerhq.github.io/LedgerWallet-doc/bitcoin-technical.html#_get_firmware_version
    public class LedgerWalletFirmware
    {
        public LedgerWalletFirmware(int major, int minor, int patch, bool compressedKeys)
        {

        }

        public LedgerWalletFirmware(byte[] bytes)
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

        public override string ToString()
        {
            return (Architecture != 0 ? "Ledger " : "") + string.Format("{0}.{1}.{2} (Loader : {3}.{4})", Major, Minor, Patch, LoaderMajor, LoaderMinor);
        }
    }
}
