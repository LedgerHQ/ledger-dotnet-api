using HidLibrary;
using LedgerWallet.Transports;
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
	public class LedgerClient : LedgerClientBase
	{
		public LedgerClient(ILedgerTransport transport) : base(transport)
		{
		}
		//https://github.com/LedgerHQ/ledger-wallet-chrome/blob/59f52dcedc031871d17cc69eb531bc6b4cf89a6b/app/libs/btchip/btchip-js-api/chromeApp/chromeDevice.js
		//https://github.com/LedgerHQ/ledger-wallet-chrome/blob/59f52dcedc031871d17cc69eb531bc6b4cf89a6b/app/src/dongle/manager.coffee
		public static IEnumerable<LedgerClient> GetHIDLedgers()
		{
			var ledgers = HIDLedgerTransport.GetHIDTransports()
							.Select(t => new LedgerClient(t))
							.ToList();
			return ledgers;
		}

		public LedgerWalletFirmware GetFirmwareVersion()
		{
			using(Transport.Lock())
			{
				byte[] response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_FIRMWARE_VERSION, (byte)0x00, (byte)0x00, 0x00, OK);
				return new LedgerWalletFirmware(response);
			}
		}

		public GetWalletPubKeyResponse GetWalletPubKey(KeyPath keyPath)
		{
			using(Transport.Lock())
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
			using(Transport.Lock())
			{
				int lastSW;
				remaining = 3;
				var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_VERIFY_PIN, 0, 0, pin.ToBytes(), out lastSW);
				if(lastSW == LedgerWalletConstants.SW_OK)
					return true;
				if(lastSW == LedgerWalletConstants.SW_INS_NOT_SUPPORTED)
					Throw(lastSW);
				remaining = (lastSW & 0x0F);
				return false;
			}
		}
		public int GetRemainingAttempts()
		{
			using(Transport.Lock())
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
			using(Transport.Lock())
			{
				var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_OPERATION_MODE, 0, 0, 0, OK);
				return (OperationMode)response[0];
			}
		}

		public SecondFactorMode GetSecondFactorMode()
		{
			using(Transport.Lock())
			{
				var response = ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_OPERATION_MODE, 1, 0, 0, OK);
				return (SecondFactorMode)response[0];
			}
		}

		public void SetOperationMode(OperationMode value)
		{
			using(Transport.Lock())
			{
				ExchangeApdu(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_SET_OPERATION_MODE, 0, 0, new[] { (byte)value }, OK);
			}
		}

		public SetupResponse RegularSetup(RegularSetup setup)
		{
			using(Transport.Lock())
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
			using(Transport.Lock())
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
			using(Transport.Lock())
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
			using(Transport.Lock())
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
			using(Transport.Lock())
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
			using(Transport.Lock())
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
