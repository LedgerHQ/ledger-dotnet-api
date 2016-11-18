using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerWallet.Transports;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Threading;

namespace LedgerWallet.U2F
{
	public class KeyHandle
	{
		private readonly byte[] _Bytes;

		public KeyHandle(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			_Bytes = bytes;
		}

		public KeyHandle(string hex)
		{
			if(hex == null)
				throw new ArgumentNullException("hex");
			_Bytes = Encoders.Hex.DecodeData(hex);
		}

		public byte[] GetBytes(bool @unsafe = false)
		{
			return @unsafe ? _Bytes : _Bytes.ToArray();
		}

		public override string ToString()
		{
			return Encoders.Hex.EncodeData(_Bytes);
		}
	}

	public class AppId
	{
		private readonly byte[] _Bytes;

		public AppId(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			_Bytes = bytes;
			if(_Bytes.Length != 32)
				throw new ArgumentException("An ApplicationId should be 32 bytes");
		}

		public AppId(string hex)
		{
			if(hex == null)
				throw new ArgumentNullException("hex");
			_Bytes = Encoders.Hex.DecodeData(hex);
			if(_Bytes.Length != 32)
				throw new ArgumentException("An ApplicationId should be 32 bytes");
		}

		public byte[] GetBytes(bool @unsafe = false)
		{
			return @unsafe ? _Bytes : _Bytes.ToArray();
		}

		public override string ToString()
		{
			return Encoders.Hex.EncodeData(_Bytes);
		}
	}

	public class U2FRegistrationResponse
	{
		public U2FRegistrationResponse(byte[] bytes)
		{
			int offset = 1;
			var pubkey = new byte[65];
			Array.Copy(bytes, offset, pubkey, 0, pubkey.Length);
			offset += pubkey.Length;
			var len = bytes[offset];
			offset++;
			var keyhandle = new byte[len];
			Array.Copy(bytes, offset, keyhandle, 0, keyhandle.Length);
			offset += keyhandle.Length;

			var certsig = new byte[bytes.Length - offset];
			Array.Copy(bytes, offset, certsig, 0, certsig.Length);

			UserPubKey = pubkey;
			KeyHandle = new KeyHandle(keyhandle);
			AttestationCertificate = new X509Certificate2(certsig);
			Signature = new byte[certsig.Length - AttestationCertificate.RawData.Length];
			Array.Copy(certsig, AttestationCertificate.RawData.Length, Signature, 0, Signature.Length);
		}
		public U2FRegistrationResponse()
		{

		}

		public byte[] UserPubKey
		{
			get; set;
		}
		public KeyHandle KeyHandle
		{
			get; set;
		}
		public X509Certificate2 AttestationCertificate
		{
			get; set;
		}
		public byte[] Signature
		{
			get; set;
		}
	}
	public class U2FClient : LedgerClientBase
	{
		const byte INS_ENROLL = 0x01;
		const byte INS_SIGN = 0x02;
		const byte INS_GET_VERSION = 0x03;


		public U2FClient(ILedgerTransport transport) : base(transport)
		{
		}

		public static IEnumerable<U2FClient> GetHIDU2F()
		{
			var ledgers = HIDU2FTransport.GetHIDTransports()
							.Select(t => new U2FClient(t))
							.ToList();
			return ledgers;
		}

		public U2FRegistrationResponse Register(AppId applicationId, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Register(RandomUtils.GetBytes(32), applicationId, cancellationToken);
		}
		public U2FRegistrationResponse Register(byte[] challenge, AppId applicationId, CancellationToken cancellationToken = default(CancellationToken))
		{
			if(challenge == null)
				throw new ArgumentNullException("challenge");
			if(challenge.Length != 32)
				throw new ArgumentException("Challenge should be 32 bytes");
			if(applicationId == null)
				throw new ArgumentNullException("applicationId");


			var data = new byte[64];
			Array.Copy(challenge, 0, data, 0, 32);
			Array.Copy(applicationId.GetBytes(true), 0, data, 32, 32);
			while(true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{

					var result = this.ExchangeApdu(INS_ENROLL, 0x03, 0x00, data);
					return new U2FRegistrationResponse(result);
				}catch(LedgerWalletException ex)
				{
					if(ex.Status.KnownSW != WellKnownSW.ConditionsOfUseNotSatisfied)
						throw;
				}
			}
		}

		private byte[] ExchangeApdu(byte ins, byte p1, byte p2, byte[] data)
		{
			MemoryStream apduStream = new MemoryStream();
			apduStream.WriteByte(0);
			apduStream.WriteByte(ins);
			apduStream.WriteByte(p1);
			apduStream.WriteByte(p2);
			apduStream.WriteByte((byte)(data.Length >> 16));
			apduStream.WriteByte((byte)(data.Length >> 8));
			apduStream.WriteByte((byte)(data.Length & 0xff));
			apduStream.Write(data, 0, data.Length);
			apduStream.WriteByte(0x04);
			apduStream.WriteByte(0);
			return ExchangeApdu(apduStream.ToArray(), OK);
		}

		protected byte[] ExchangeApduNoDataLength(byte cla, byte ins, byte p1, byte p2, byte[] data, out int sw)
		{
			sw = 0;
			byte[] apdu = new byte[data.Length + 5];
			apdu[0] = cla;
			apdu[1] = ins;
			apdu[2] = p1;
			apdu[3] = p2;
			Array.Copy(data, 0, apdu, 4, data.Length);
			return Exchange(apdu, out sw);
		}

	}
}
