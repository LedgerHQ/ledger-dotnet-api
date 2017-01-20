using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerWallet.Transports;

namespace LedgerWallet
{
	public abstract class LedgerClientBase
	{
		public static int[] OK = new[] { LedgerWalletConstants.SW_OK };
		readonly ILedgerTransport _Transport;
		public ILedgerTransport Transport
		{
			get
			{
				return _Transport;
			}
		}

		public LedgerClientBase(ILedgerTransport transport)
		{
			if(transport == null)
				throw new ArgumentNullException("transport");
			this._Transport = transport;
		}

		protected byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
		{
			int sw;
			var response = ExchangeApdu(cla, ins, p1, p2, data, out sw);
			CheckSW(acceptedSW, sw);
			return response;
		}

		protected byte[] ExchangeApduSplit(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
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

		protected byte[] ExchangeApduSplit2(byte cla, byte ins, byte p1, byte p2, byte[] data, byte[] data2, int[] acceptedSW)
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

		protected void Throw(int sw)
		{
			var status = new LedgerWalletStatus(sw);
			throw new LedgerWalletException(GetErrorMessage(status), status);
		}

		protected void CheckSW(int[] acceptedSW, int sw)
		{
			if(!acceptedSW.Contains(sw))
			{
				Throw(sw);
			}
		}

		protected virtual string GetErrorMessage(LedgerWalletStatus status)
		{
			switch(status.SW)
			{
				case 0x6D00:
					return "INS not supported";
				case 0x6E00:
					return "CLA not supported";
				case 0x6700:
					return "Incorrect length";
				case 0x6982:
					return "Command not allowed : Security status not satisfied";
				case 0x6985:
					return "Command not allowed : Conditions of use not satisfied";
				case 0x6A80:
					return "Invalid data";
				case 0x6482:
					return "File not found";
				case 0x6B00:
					return "Incorrect parameter P1 or P2";
				case 0x9000:
					return "OK";
				case 0x6D00:
					return "Insupported command";
				default:
					{
						if((status.SW & 0xFF00) != 0x6F00)
							return "Unknown error";
						switch(status.InternalSW)
						{
							case 0xAA:
								return "The dongle must be reinserted";
							default:
								return "Unknown error";
						}
					}
			}
		}

		protected byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, byte[] data, out int sw)
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

		protected byte[] ExchangeApdu(byte cla, byte ins, byte p1, byte p2, int length, int[] acceptedSW)
		{
			byte[] apdu = new byte[]
			{
				cla,ins,p1,p2,(byte)length
			};
			return ExchangeApdu(apdu, acceptedSW);
		}

		protected byte[] ExchangeApdu(byte[] apdu, int[] acceptedSW)
		{
			int sw;
			var resp = Exchange(apdu, out sw);
			CheckSW(acceptedSW, sw);
			return resp;
		}

		protected byte[] Exchange(byte[] apdu, out int sw)
		{
			byte[] response = Transport.Exchange(apdu);
			if(response.Length < 2)
			{
				throw new LedgerWalletException("Truncated response");
			}
			sw = ((int)(response[response.Length - 2] & 0xff) << 8) |
					(int)(response[response.Length - 1] & 0xff);
			if(sw == 0x6faa)
				Throw(sw);
			byte[] result = new byte[response.Length - 2];
			Array.Copy(response, 0, result, 0, response.Length - 2);
			return result;
		}
	}
}
