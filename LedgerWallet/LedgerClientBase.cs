using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerWallet.Transports;

namespace LedgerWallet
{
	public class APDUResponse
	{
		public byte[] Response
		{
			get; set;
		}
		public int SW
		{
			get; set;
		}
	}
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

		protected byte[] CreateAPDU(byte cla, byte ins, byte p1, byte p2, byte[] data)
		{
			byte[] apdu = new byte[data.Length + 5];
			apdu[0] = cla;
			apdu[1] = ins;
			apdu[2] = p1;
			apdu[3] = p2;
			apdu[4] = (byte)(data.Length);
			Array.Copy(data, 0, apdu, 5, data.Length);
			return apdu;
		}

		protected byte[][] CreateAPDUSplit(byte cla, byte ins, byte p1, byte p2, byte[] data)
		{
			int offset = 0;
			List<byte[]> result = new List<byte[]>();
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
				result.Add(apdu);
				offset += blockLength;
			}
			return result.ToArray();
		}

		protected byte[][] CreateApduSplit2(byte cla, byte ins, byte p1, byte p2, byte[] data, byte[] data2)
		{
			int offset = 0;
			int maxBlockSize = 255 - data2.Length;
			List<byte[]> apdus = new List<byte[]>();
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
				apdus.Add(apdu);
				offset += blockLength;
			}
			return apdus.ToArray();
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


		protected Task<byte[]> ExchangeSingleAPDU(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW)
		{
			return ExchangeApdus(new byte[][] { CreateAPDU(cla, ins, p1, p2, data) }, acceptedSW);
		}

		protected Task<APDUResponse> ExchangeSingleAPDU(byte cla, byte ins, byte p1, byte p2, byte[] data)
		{
			return ExchangeSingle(new byte[][] { CreateAPDU(cla, ins, p1, p2, data) });
		}

		protected Task<byte[]> ExchangeSingleAPDU(byte cla, byte ins, byte p1, byte p2, int length, int[] acceptedSW)
		{
			byte[] apdu = new byte[]
			{
				cla,ins,p1,p2,(byte)length
			};
			return ExchangeApdus(new byte[][] { apdu }, acceptedSW);
		}

		protected async Task<byte[]> ExchangeApdus(byte[][] apdus, int[] acceptedSW)
		{
			var resp = await ExchangeSingle(apdus).ConfigureAwait(false);
			CheckSW(acceptedSW, resp.SW);
			return resp.Response;
		}

		protected async Task<APDUResponse> ExchangeSingle(byte[][] apdus)
		{
			var responses = await Exchange(apdus).ConfigureAwait(false);
			var last = responses.Last();
			foreach(var response in responses)
			{
				if(response != last)
					CheckSW(OK, response.SW);
			}
			return last;
		}
		protected async Task<APDUResponse[]> Exchange(byte[][] apdus)
		{
			byte[][] responses = await Transport.Exchange(apdus).ConfigureAwait(false);
			List<APDUResponse> resultResponses = new List<APDUResponse>();
			foreach(var response in responses)
			{
				if(response.Length < 2)
				{
					throw new LedgerWalletException("Truncated response");
				}
				int sw = ((int)(response[response.Length - 2] & 0xff) << 8) |
						(int)(response[response.Length - 1] & 0xff);
				if(sw == 0x6faa)
					Throw(sw);
				byte[] result = new byte[response.Length - 2];
				Array.Copy(response, 0, result, 0, response.Length - 2);
				resultResponses.Add(new APDUResponse() { Response = result, SW = sw });
			}
			return resultResponses.ToArray();
		}
	}
}
