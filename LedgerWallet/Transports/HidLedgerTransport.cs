using HidLibrary;
using Microsoft.Win32.SafeHandles;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public class VendorProductIds
	{
		public VendorProductIds(int vendorId)
		{
			VendorId = vendorId;
		}
		public VendorProductIds(int vendorId, int? productId)
		{
			VendorId = vendorId;
			ProductId = productId;
		}
		public int VendorId
		{
			get; set;
		}
		public int? ProductId
		{
			get; set;
		}
	}
	public class HIDLedgerTransport : ILedgerTransport
	{
		internal HidDevice _Device;
		const int TAG_APDU = 0x05;
		readonly string _DevicePath;
		readonly VendorProductIds _VendorProductIds;

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, int dwFlags);

		static HIDLedgerTransport()
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


		public string DevicePath
		{
			get
			{
				return _DevicePath;
			}
		}

		public static VendorProductIds[] WellKnownLedgerWallets = new VendorProductIds[]
		{
			new VendorProductIds(0x2c97),
			new VendorProductIds(0x2581, 0x3b7c)
		};

		public static unsafe IEnumerable<HIDLedgerTransport> GetHIDTransports(IEnumerable<VendorProductIds> ids = null)
		{
			ids = ids ?? WellKnownLedgerWallets;
			return EnumerateHIDDevices(ids)
							.Select(d => GetTransport(d))
							.ToList();
		}

		static Dictionary<string, HIDLedgerTransport> _TransportsByDevicePath = new Dictionary<string, HIDLedgerTransport>();
		static HIDLedgerTransport GetTransport(HidDevice device)
		{
			lock(_TransportsByDevicePath)
			{
				HIDLedgerTransport transport = null;
				var uniqueId = string.Format("[{0},{1}]{2}", device.Attributes.VendorId, device.Attributes.ProductId, device.DevicePath);
				if(_TransportsByDevicePath.TryGetValue(uniqueId, out transport))
					return transport;
				transport = new HIDLedgerTransport(device);
				_TransportsByDevicePath.Add(uniqueId, transport);
				return transport;
			}
		}

		private HIDLedgerTransport(HidDevice device)
		{
			if(!device.IsOpen)
				device.OpenDevice();
			_Device = device;
			_DevicePath = device.DevicePath;
			_VendorProductIds = new VendorProductIds(device.Attributes.VendorId, device.Attributes.ProductId);
		}
		
		internal byte[] ExchangeCore(byte[] apdu)
		{
			MemoryStream output = new MemoryStream();
			byte[] buffer = new byte[400];
			byte[] paddingBuffer = new byte[MAX_BLOCK];
			int result;
			int length;
			int swOffset;
			uint remaining = (uint)apdu.Length;
			uint offset = 0;

			result = WrapCommandAPDU(DEFAULT_LEDGER_CHANNEL, apdu, LEDGER_HID_PACKET_SIZE, buffer);
			if(result < 0)
			{
				return null;
			}
			remaining = (uint)result;

			while(remaining > 0)
			{
				uint blockSize = (remaining > MAX_BLOCK ? MAX_BLOCK : remaining);
				memset(paddingBuffer, 0, MAX_BLOCK);
				memcpy(paddingBuffer, 0U, buffer, offset, blockSize);

				result = hid_write(_Device.Handle, paddingBuffer, (int)blockSize);
				if(result < 0)
				{
					return null;
				}
				offset += blockSize;
				remaining -= blockSize;
			}

			buffer = new byte[400];
			result = hid_read_timeout(_Device.Handle, buffer, MAX_BLOCK, TIMEOUT);
			if(result < 0)
			{
				return null;
			}
			offset = MAX_BLOCK;
			for(;;)
			{
				output = new MemoryStream();
				result = UnwrapReponseAPDU(DEFAULT_LEDGER_CHANNEL, buffer, offset, LEDGER_HID_PACKET_SIZE, output);
				if(result < 0)
				{
					return null;
				}
				if(result != 0)
				{
					length = result - 2;
					swOffset = result - 2;
					break;
				}
				result = hid_read_timeout(_Device.Handle, buffer, offset, MAX_BLOCK, TIMEOUT);
				if(result < 0)
				{
					return null;
				}
				offset += MAX_BLOCK;
			}
			return output.ToArray();
		}

		private int hid_read_timeout(IntPtr intPtr, byte[] buffer, uint offset, uint length, int milliseconds)
		{
			var bytes = new byte[length];
			Array.Copy(buffer, offset, bytes, 0, length);
			var result = hid_read_timeout(intPtr, bytes, (uint)length, milliseconds);
			Array.Copy(bytes, 0, buffer, offset, length);
			return result;
		}



		internal int hid_read_timeout(IntPtr hidDeviceObject, byte[] buffer, uint length, int milliseconds)
		{
			var result = this._Device.Read(milliseconds);
			if(result.Status == HidDeviceData.ReadStatus.Success)
			{
				if(result.Data.Length - 1 > length)
					return -1;
				Array.Copy(result.Data, 1, buffer, 0, length);
				return result.Data.Length;
			}
			return -1;
		}

		internal int hid_write(IntPtr hidDeviceObject, byte[] buffer, int length)
		{
			byte[] sent = new byte[length + 1];
			Array.Copy(buffer, 0, sent, 1, length);
			if(!this._Device.Write(sent))
				return -1;
			Array.Copy(sent, 0, buffer, 0, length);
			return length;
		}


		private void memset(byte[] array, byte value, uint count)
		{
			for(int i = 0; i < count; i++)
				array[i] = value;
		}



		const uint MAX_BLOCK = 64;
		const int VID = 0x2581;
		const int PID = 0x2b7c;
		const int PID_LEDGER = 0x3b7c;
		const int PID_LEDGER_PROTON = 0x4b7c;
		const int HID_BUFFER_SIZE = 64;
		const int SW1_DATA_AVAILABLE = 0x61;
		const int DEFAULT_LEDGER_CHANNEL = 0x0101;
		const int LEDGER_HID_PACKET_SIZE = 64;
		const int TIMEOUT = 20000;


		int WrapCommandAPDU(uint channel, byte[] command, uint packetSize, byte[] output)
		{
			uint commandLength = (uint)command.Length;
			uint outputLength = (uint)output.Length;
			int sequenceIdx = 0;
			uint offset = 0;
			uint offsetOut = 0;
			uint blockSize;
			if(packetSize < 3)
			{
				return -1;
			}
			if(outputLength < 7)
			{
				return -1;
			}
			outputLength -= 7;
			output[offsetOut++] = (byte)((channel >> 8) & 0xff);
			output[offsetOut++] = (byte)(channel & 0xff);
			output[offsetOut++] = (byte)TAG_APDU;
			output[offsetOut++] = (byte)((sequenceIdx >> 8) & 0xff);
			output[offsetOut++] = (byte)(sequenceIdx & 0xff);
			sequenceIdx++;
			output[offsetOut++] = (byte)((commandLength >> 8) & 0xff);
			output[offsetOut++] = (byte)(commandLength & 0xff);
			blockSize = (commandLength > packetSize - 7 ? packetSize - 7 : commandLength);
			if(outputLength < blockSize)
			{
				return -1;
			}
			outputLength -= blockSize;
			memcpy(output, offsetOut, command, offset, blockSize);
			offsetOut += blockSize;
			offset += blockSize;
			while(offset != commandLength)
			{
				if(outputLength < 5)
				{
					return -1;
				}
				outputLength -= 5;
				output[offsetOut++] = (byte)((channel >> 8) & 0xff);
				output[offsetOut++] = (byte)(channel & 0xff);
				output[offsetOut++] = (byte)TAG_APDU;
				output[offsetOut++] = (byte)((sequenceIdx >> 8) & 0xff);
				output[offsetOut++] = (byte)(sequenceIdx & 0xff);
				sequenceIdx++;
				blockSize = ((commandLength - offset) > packetSize - 5 ? packetSize - 5 : commandLength - offset);
				if(outputLength < blockSize)
				{
					return -1;
				}
				outputLength -= blockSize;
				memcpy(output, offsetOut, command, offset, blockSize);
				offsetOut += blockSize;
				offset += blockSize;
			}
			while((offsetOut % packetSize) != 0)
			{
				if(outputLength < 1)
				{
					return -1;
				}
				outputLength--;
				output[offsetOut++] = 0;
			}
			return (int)offsetOut;
		}

		private void memcpy(byte[] dest, uint destOffset, byte[] src, uint srcOffset, uint length)
		{
			Array.Copy(src, srcOffset, dest, destOffset, length);
		}

		private void memcpy(Stream dest, byte[] src, uint srcOffset, uint length)
		{
			dest.Write(src, (int)srcOffset, (int)length);
		}

		int UnwrapReponseAPDU(uint channel, byte[] data, uint dataLength, uint packetSize, Stream output)
		{
			int sequenceIdx = 0;
			uint offset = 0;
			uint offsetOut = 0;
			uint responseLength;
			uint blockSize;
			if((data == null) || (dataLength < 7 + 5))
			{
				return 0;
			}
			if(data[offset++] != ((channel >> 8) & 0xff))
			{
				return -1;
			}
			if(data[offset++] != (channel & 0xff))
			{
				return -1;
			}
			if(data[offset++] != TAG_APDU)
			{
				return -1;
			}
			if(data[offset++] != ((sequenceIdx >> 8) & 0xff))
			{
				return -1;
			}
			if(data[offset++] != (sequenceIdx & 0xff))
			{
				return -1;
			}
			responseLength = (((uint)data[offset++]) << 8);
			responseLength |= data[offset++];

			if(dataLength < 7 + responseLength)
			{
				return 0;
			}
			blockSize = (responseLength > packetSize - 7 ? packetSize - 7 : responseLength);
			memcpy(output, data, offset, blockSize);
			offset += blockSize;
			offsetOut += blockSize;
			while(offsetOut != responseLength)
			{
				sequenceIdx++;
				if(offset == dataLength)
				{
					return 0;
				}
				if(data[offset++] != ((channel >> 8) & 0xff))
				{
					return -1;
				}
				if(data[offset++] != (channel & 0xff))
				{
					return -1;
				}
				if(data[offset++] != TAG_APDU)
				{
					return -1;
				}
				if(data[offset++] != ((sequenceIdx >> 8) & 0xff))
				{
					return -1;
				}
				if(data[offset++] != (sequenceIdx & 0xff))
				{
					return -1;
				}
				blockSize = ((responseLength - offsetOut) > packetSize - 5 ? packetSize - 5 : responseLength - offsetOut);
				if(blockSize > dataLength - offset)
				{
					return 0;
				}
				memcpy(output, data, offset, blockSize);
				offset += blockSize;
				offsetOut += blockSize;
			}
			return (int)offsetOut;
		}

		private static unsafe IEnumerable<HidDevice> EnumerateHIDDevices(IEnumerable<VendorProductIds> vendorProductIds)
		{
			List<HidDevice> devices = new List<HidDevice>();
			foreach(var ids in vendorProductIds)
			{
				if(ids.ProductId == null)
					devices.AddRange(HidDevices.Enumerate(ids.VendorId));
				else
					devices.AddRange(HidDevices.Enumerate(ids.VendorId, ids.ProductId.Value));

			}
			return devices;
		}

		DisposableLock l = new DisposableLock();
		public IDisposable Lock()
		{
			return l.Lock();
		}

		public byte[] Exchange(byte[] apdu)
		{
			var response = ExchangeCore(apdu);
			if(response == null)
			{
				if(!RenewTransport())
				{
					throw new LedgerWalletException("Ledger disconnected");
				}
				response = ExchangeCore(apdu);
				if(response == null)
					throw new LedgerWalletException("Error while transmission");
			}
			return response;
		}

		private bool RenewTransport()
		{
			var newDevice = EnumerateHIDDevices(new[]
			{
				this._VendorProductIds
			})
			.FirstOrDefault(hid => hid.DevicePath == _DevicePath);
			if(newDevice == null)
				return false;
			_Device = newDevice;
			if(!_Device.IsOpen)
				_Device.OpenDevice();
			return true;
		}
	}

}
