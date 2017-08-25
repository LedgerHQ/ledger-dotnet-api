using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerWallet.Transports;
using System.IO;

namespace LedgerWallet
{
	/// <summary>
	/// LedgerClient for Ledger Nano and HW.1
	/// </summary>
	public class LegacyLedgerClient : LedgerClient
	{
		public LegacyLedgerClient(ILedgerTransport transport) : base(transport)
		{
		}

		public static new IEnumerable<LegacyLedgerClient> GetHIDLedgers()
		{
			var ledgers = HIDLedgerTransport.GetHIDTransports()
							.Select(t => new LegacyLedgerClient(t))
							.ToList();
			return ledgers;
		}

		public bool VerifyPin(string pin, out int remaining)
		{
			return VerifyPin(new UserPin(pin), out remaining);
		}
		public bool VerifyPin(UserPin pin, out int remaining)
		{
			remaining = 3;
			var response = ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_VERIFY_PIN, 0, 0, pin.ToBytes()).GetAwaiter().GetResult();
			if(response.SW == LedgerWalletConstants.SW_OK)
				return true;
			if(response.SW == LedgerWalletConstants.SW_INS_NOT_SUPPORTED)
				Throw(response.SW);
			remaining = (response.SW & 0x0F);
			return false;
		}
		public int GetRemainingAttempts()
		{
			var response = ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_VERIFY_PIN, 0x80, 0, new byte[] { 1 }).GetAwaiter().GetResult();
			return (response.SW & 0x0F);
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
			return GetOperationModeAsync().GetAwaiter().GetResult();
		}
		public async Task<OperationMode> GetOperationModeAsync()
		{
			var response = await ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_OPERATION_MODE, 0, 0, 0, OK).ConfigureAwait(false);
			return (OperationMode)response[0];
		}

		public async Task<SecondFactorMode> GetSecondFactorModeAsync()
		{
			var response = await ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_OPERATION_MODE, 1, 0, 0, OK).ConfigureAwait(false);
			return (SecondFactorMode)response[0];
		}
		public SecondFactorMode GetSecondFactorMode()
		{
			return GetSecondFactorModeAsync().GetAwaiter().GetResult();
		}

		public void SetOperationMode(OperationMode value)
		{
			ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_SET_OPERATION_MODE, 0, 0, new[] { (byte)value }, OK);
		}

		public SetupResponse RegularSetup(RegularSetup setup)
		{
			return RegularSetupAsync(setup).GetAwaiter().GetResult();
		}
		public async Task<SetupResponse> RegularSetupAsync(RegularSetup setup)
		{
			var response = await ExchangeSingleAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_SETUP, 0, 0, setup.ToBytes(), OK).ConfigureAwait(false);
			return new SetupResponse(response);
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
}
