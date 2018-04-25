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
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet
{
	public class LedgerClient : LedgerClientBase
	{
		public LedgerClient(ILedgerTransport transport) : base(transport)
		{
		}
		public static IEnumerable<LedgerClient> GetHIDLedgers()
		{
			var ledgers = HIDLedgerTransport.GetHIDTransports()
							.Select(t => new LedgerClient(t))
							.ToList();
			return ledgers;
		}

		public async Task<LedgerWalletFirmware> GetFirmwareVersionAsync()
		{
			byte[] response = await ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_FIRMWARE_VERSION, (byte)0x00, (byte)0x00, 0x00, OK).ConfigureAwait(false);
			return new LedgerWalletFirmware(response);
		}
		public LedgerWalletFirmware GetFirmwareVersion()
		{
			return GetFirmwareVersionAsync().GetAwaiter().GetResult();
		}
		public GetWalletPubKeyResponse GetWalletPubKey(KeyPath keyPath)
		{
			return GetWalletPubKeyAsync(keyPath).GetAwaiter().GetResult();
		}
		public GetWalletPubKeyResponse GetWalletPubKey(KeyPath keyPath, AddressType displayMode = AddressType.Legacy, bool display = false)
		{
			return GetWalletPubKeyAsync(keyPath, displayMode, display).GetAwaiter().GetResult();
		}

		[Flags]
		public enum AddressType
		{
			Legacy = 0x00,
			Segwit = 0x01,
			NativeSegwit = 0x02,
		}

		public async Task<GetWalletPubKeyResponse> GetWalletPubKeyAsync(KeyPath keyPath, AddressType addressType = AddressType.Legacy, bool display = false)
		{
			Guard.AssertKeyPath(keyPath);
			byte[] bytes = Serializer.Serialize(keyPath);
			//bytes[0] = 10;
			byte[] response = await ExchangeSingleAPDU(
				LedgerWalletConstants.LedgerWallet_CLA,
				LedgerWalletConstants.LedgerWallet_INS_GET_WALLET_PUBLIC_KEY,
				(byte)(display ? 1 : 0),
				(byte)addressType, bytes, OK).ConfigureAwait(false);
			return new GetWalletPubKeyResponse(response);
		}

		public GetWalletPubKeyResponse GetWalletMasterKey()
		{
			return GetWalletMasterKeyAsync().GetAwaiter().GetResult();
		}

		public async Task<GetWalletPubKeyResponse> GetWalletMasterKeyAsync()
		{
			return await GetWalletPubKeyAsync(new KeyPath("0'"));
		}

		public KeyPath GetWalletHDKeyPathForSegwitAddress(KeyPath rootKeyPath, string segwitAddress, Network network, int startAtIndex = 0, int maxAttempts = 100)
		{
			return GetWalletHDKeyPathForSegwitAddressAsync(rootKeyPath, segwitAddress, network, startAtIndex, maxAttempts).GetAwaiter().GetResult();
		}

		public async Task<KeyPath> GetWalletHDKeyPathForSegwitAddressAsync(KeyPath rootKeyPath, string segwitAddress, Network network, int startAtIndex = 0, int? maxAttempts = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			Guard.AssertKeyPath(rootKeyPath);

			GetWalletPubKeyResponse response = await GetWalletPubKeyAsync(rootKeyPath);
			ExtPubKey hdKey = response.ExtendedPublicKey;

			var i = startAtIndex;
			var a = 0L;

			while(true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var keyPath = new KeyPath($"0/{i}");

				var segwit = $"{hdKey.Derive(keyPath).PubKey.WitHash.ScriptPubKey.Hash.GetAddress(network)}";
				if(segwit == segwitAddress)
				{
					return keyPath;
				}

				i++;
				a++;

				if(a > maxAttempts)
				{
					return null;
				}
			}
		}

		public Task<TrustedInput> GetTrustedInputAsync(IndexedTxOut txout)
		{
			return GetTrustedInputAsync(txout.Transaction, (int)txout.N);
		}

		public TrustedInput GetTrustedInput(IndexedTxOut txout)
		{
			return GetTrustedInputAsync(txout.Transaction, (int)txout.N).GetAwaiter().GetResult();
		}

		public TrustedInput GetTrustedInput(Transaction transaction, int outputIndex)
		{
			return GetTrustedInputAsync(transaction, outputIndex).GetAwaiter().GetResult();
		}

		public async Task<TrustedInput> GetTrustedInputAsync(Transaction transaction, int outputIndex)
		{
			if(outputIndex >= transaction.Outputs.Count)
				throw new ArgumentOutOfRangeException("outputIndex is bigger than the number of outputs in the transaction", "outputIndex");
			List<byte[]> apdus = new List<byte[]>();
			MemoryStream data = new MemoryStream();
			// Header
			BufferUtils.WriteUint32BE(data, outputIndex);
			BufferUtils.WriteBuffer(data, transaction.Version);
			VarintUtils.write(data, transaction.Inputs.Count);
			apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x00, (byte)0x00, data.ToArray()));
			// Each input
			foreach(var input in transaction.Inputs)
			{
				data = new MemoryStream();
				BufferUtils.WriteBuffer(data, input.PrevOut);
				VarintUtils.write(data, input.ScriptSig.Length);
				apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray()));
				data = new MemoryStream();
				BufferUtils.WriteBuffer(data, input.ScriptSig.ToBytes());
				apdus.AddRange(CreateApduSplit2(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray(), Utils.ToBytes(input.Sequence, true)));
			}
			// Number of outputs
			data = new MemoryStream();
			VarintUtils.write(data, transaction.Outputs.Count);
			apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray()));
			// Each output
			foreach(var output in transaction.Outputs)
			{
				data = new MemoryStream();
				BufferUtils.WriteBuffer(data, Utils.ToBytes((ulong)output.Value.Satoshi, true));
				VarintUtils.write(data, output.ScriptPubKey.Length);
				apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray()));
				data = new MemoryStream();
				BufferUtils.WriteBuffer(data, output.ScriptPubKey.ToBytes());
				apdus.AddRange(CreateAPDUSplit(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, data.ToArray()));
			}
			// Locktime
			apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, (byte)0x80, (byte)0x00, transaction.LockTime.ToBytes()));
			byte[] response = await ExchangeApdus(apdus.ToArray(), OK).ConfigureAwait(false);
			return new TrustedInput(response);
		}

		public enum InputStartType
		{
			New = 0x00,
			NewSegwit = 0x02,
			Continue = 0x80
		}

		public byte[][] UntrustedHashTransactionInputStart(
			InputStartType startType,
			IndexedTxIn txIn,
			Dictionary<OutPoint, TrustedInput> trustedInputs,
			Dictionary<OutPoint, ICoin> coins,
			bool segwitMode, bool segwitParsedOnce)
		{
			List<byte[]> apdus = new List<byte[]>();
			trustedInputs = trustedInputs ?? new Dictionary<OutPoint, TrustedInput>();
			// Start building a fake transaction with the passed inputs
			MemoryStream data = new MemoryStream();
			BufferUtils.WriteBuffer(data, txIn.Transaction.Version);

			if(segwitMode && segwitParsedOnce)
				VarintUtils.write(data, 1);
			else
				VarintUtils.write(data, txIn.Transaction.Inputs.Count);

			apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, (byte)0x00, (byte)startType, data.ToArray()));
			// Loop for each input
			long currentIndex = 0;
			foreach(var input in txIn.Transaction.Inputs)
			{
				if(segwitMode && segwitParsedOnce && currentIndex != txIn.Index)
				{
					currentIndex++;
					continue;
				}
				byte[] script = new byte[0];
				if(currentIndex == txIn.Index || segwitMode && !segwitParsedOnce)
					script = coins[input.PrevOut].GetScriptCode().ToBytes();

				data = new MemoryStream();
				if(segwitMode)
				{
					data.WriteByte(0x02);
					BufferUtils.WriteBuffer(data, input.PrevOut);
					BufferUtils.WriteBuffer(data, Utils.ToBytes((ulong)coins[input.PrevOut].TxOut.Value.Satoshi, true));
				}
				else
				{
					var trustedInput = trustedInputs[input.PrevOut];
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
				}
				VarintUtils.write(data, script.Length);
				apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, (byte)0x80, (byte)0x00, data.ToArray()));
				data = new MemoryStream();
				BufferUtils.WriteBuffer(data, script);
				BufferUtils.WriteBuffer(data, input.Sequence);
				apdus.AddRange(CreateAPDUSplit(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, (byte)0x80, (byte)0x00, data.ToArray()));
				currentIndex++;
			}
			return apdus.ToArray();
		}

		public byte[][] UntrustedHashTransactionInputFinalizeFull(KeyPath change, IEnumerable<TxOut> outputs)
		{
			List<byte[]> apdus = new List<byte[]>();
			if(change != null)
			{
				apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_FINALIZE_FULL, 0xFF, 0x00, Serializer.Serialize(change)));
			}

			int offset = 0;
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
				apdus.Add(apdu);
				offset += blockLength;
			}
			return apdus.ToArray();
		}
		public Transaction SignTransaction(KeyPath keyPath, ICoin[] signedCoins, Transaction[] parents, Transaction transaction, KeyPath changePath = null)
		{
			return SignTransactionAsync(keyPath, signedCoins, parents, transaction, changePath).GetAwaiter().GetResult();
		}
		public Task<Transaction> SignTransactionAsync(KeyPath keyPath, ICoin[] signedCoins, Transaction[] parents, Transaction transaction, KeyPath changePath = null)
		{
			List<SignatureRequest> requests = new List<SignatureRequest>();
			foreach(var c in signedCoins)
			{
				var tx = parents.FirstOrDefault(t => t.GetHash() == c.Outpoint.Hash);
				if(tx != null)
					requests.Add(new SignatureRequest()
					{
						InputCoin = c,
						InputTransaction = tx,
						KeyPath = keyPath
					});
			}
			return SignTransactionAsync(requests.ToArray(), transaction, changePath: changePath);
		}
		public Transaction SignTransaction(SignatureRequest[] signatureRequests, Transaction transaction, KeyPath changePath = null)
		{
			return SignTransactionAsync(signatureRequests, transaction, changePath).GetAwaiter().GetResult();
		}
		public async Task<Transaction> SignTransactionAsync(SignatureRequest[] signatureRequests, Transaction transaction, KeyPath changePath = null)
		{
			if(signatureRequests.Length == 0)
				throw new ArgumentException("No signatureRequests is passed", "signatureRequests");
			var segwitCoins = signatureRequests.Where(s => s.InputCoin.GetHashVersion() == HashVersion.Witness).Count();
			if(segwitCoins != signatureRequests.Count() && segwitCoins != 0)
				throw new ArgumentException("Mixing segwit input with non segwit input is not supported", "signatureRequests");

			var segwitMode = segwitCoins != 0;

			Dictionary<OutPoint, SignatureRequest> requests = signatureRequests
				.ToDictionaryUnique(o => o.InputCoin.Outpoint);
			transaction = transaction.Clone();
			Dictionary<OutPoint, IndexedTxIn> inputsByOutpoint = transaction.Inputs.AsIndexedInputs().ToDictionary(i => i.PrevOut);
			Dictionary<OutPoint, ICoin> coinsByOutpoint = requests.ToDictionary(o => o.Key, o => o.Value.InputCoin);

			List<Task<TrustedInput>> trustedInputsAsync = new List<Task<TrustedInput>>();
			if(!segwitMode)
			{
				foreach(var sigRequest in signatureRequests)
				{
					trustedInputsAsync.Add(GetTrustedInputAsync(sigRequest.InputTransaction, (int)sigRequest.InputCoin.Outpoint.N));
				}
			}

			var noPubKeyRequests = signatureRequests.Where(r => r.PubKey == null).ToArray();
			List<Task<GetWalletPubKeyResponse>> getPubKeys = new List<Task<GetWalletPubKeyResponse>>();
			foreach(var previousReq in noPubKeyRequests)
			{
				getPubKeys.Add(GetWalletPubKeyAsync(previousReq.KeyPath));
			}
			await Task.WhenAll(getPubKeys).ConfigureAwait(false);
			await Task.WhenAll(trustedInputsAsync).ConfigureAwait(false);

			for(int i = 0; i < noPubKeyRequests.Length; i++)
			{
				noPubKeyRequests[i].PubKey = getPubKeys[i].Result.UncompressedPublicKey.Compress();
			}

			var trustedInputs = trustedInputsAsync.Select(t => t.Result).ToDictionaryUnique(i => i.OutPoint);
			List<byte[]> apdus = new List<byte[]>();
			InputStartType inputStartType = segwitMode ? InputStartType.NewSegwit : InputStartType.New;


			bool segwitParsedOnce = false;
			for(int i = 0; i < signatureRequests.Length; i++)
			{
				var sigRequest = signatureRequests[i];
				var input = inputsByOutpoint[sigRequest.InputCoin.Outpoint];
				apdus.AddRange(UntrustedHashTransactionInputStart(inputStartType, input, trustedInputs, coinsByOutpoint, segwitMode, segwitParsedOnce));
				inputStartType = InputStartType.Continue;
				if(!segwitMode || !segwitParsedOnce)
					apdus.AddRange(UntrustedHashTransactionInputFinalizeFull(changePath, transaction.Outputs));
				changePath = null; //do not resubmit the changepath
				if(segwitMode && !segwitParsedOnce)
				{
					segwitParsedOnce = true;
					i--; //pass once more
					continue;
				}
				apdus.Add(UntrustedHashSign(sigRequest.KeyPath, null, transaction.LockTime, SigHash.All));
			}
			var responses = await Exchange(apdus.ToArray()).ConfigureAwait(false);
			foreach(var response in responses)
				if(response.Response.Length > 10) //Probably a signature
					response.Response[0] = 0x30;
			var signatures = responses.Where(p => TransactionSignature.IsValid(p.Response)).Select(p => new TransactionSignature(p.Response)).ToArray();

			if(signatureRequests.Length != signatures.Length)
				throw new LedgerWalletException("failed to sign some inputs");
			int sigIndex = 0;

			TransactionBuilder builder = new TransactionBuilder();
			foreach(var sigRequest in signatureRequests)
			{
				var input = inputsByOutpoint[sigRequest.InputCoin.Outpoint];
				if(input == null)
					continue;
				builder.AddCoins(sigRequest.InputCoin);
				builder.AddKnownSignature(sigRequest.PubKey, signatures[sigIndex]);
				sigIndex++;
			}
			builder.SignTransactionInPlace(transaction);

			sigIndex = 0;
			foreach(var sigRequest in signatureRequests)
			{
				var input = inputsByOutpoint[sigRequest.InputCoin.Outpoint];
				if(input == null)
					continue;
				sigRequest.Signature = signatures[sigIndex];
				if(!sigRequest.PubKey.Verify(transaction.GetSignatureHash(sigRequest.InputCoin, sigRequest.Signature.SigHash), sigRequest.Signature.Signature))
				{
					foreach(var sigRequest2 in signatureRequests)
						sigRequest2.Signature = null;
					return null;
				}
				sigIndex++;
			}

			return transaction;
		}

		private async Task ModifyScriptSigAndVerifySignature(Task<TransactionSignature> sigTask, SignatureRequest previousReq, IndexedTxIn input)
		{
			var pubkey = previousReq.PubKey ??
						(await GetWalletPubKeyAsync(previousReq.KeyPath).ConfigureAwait(false)).UncompressedPublicKey.Compress();

			var sig = await sigTask.ConfigureAwait(false);
		}

		public byte[] UntrustedHashSign(KeyPath keyPath, UserPin pin, LockTime lockTime, SigHash sigHashType)
		{
			MemoryStream data = new MemoryStream();
			byte[] path = Serializer.Serialize(keyPath);
			BufferUtils.WriteBuffer(data, path);

			var pinBytes = pin == null ? new byte[0] : pin.ToBytes();
			data.WriteByte((byte)pinBytes.Length);
			BufferUtils.WriteBuffer(data, pinBytes);
			BufferUtils.WriteUint32BE(data, (uint)lockTime);
			data.WriteByte((byte)sigHashType);
			return CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_SIGN, (byte)0x00, (byte)0x00, data.ToArray());
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
