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

		public static VendorProductIds[] WellKnownU2F = new VendorProductIds[]
		{
				new VendorProductIds(0x1050, 0x0200),  // Gnubby
				new VendorProductIds(0x1050, 0x0113),  // YubiKey NEO U2F
				new VendorProductIds(0x1050, 0x0114),  // YubiKey NEO OTP+U2F
				new VendorProductIds(0x1050, 0x0115),  // YubiKey NEO U2F+CCID
				new VendorProductIds(0x1050, 0x0116),  // YubiKey NEO OTP+U2F+CCID
				new VendorProductIds(0x1050, 0x0120),  // Security Key by Yubico
				new VendorProductIds(0x1050, 0x0410),  // YubiKey Plus
				new VendorProductIds(0x1050, 0x0402),  // YubiKey 4 U2F
				new VendorProductIds(0x1050, 0x0403),  // YubiKey 4 OTP+U2F
				new VendorProductIds(0x1050, 0x0406),  // YubiKey 4 U2F+CCID
				new VendorProductIds(0x1050, 0x0407),  // YubiKey 4 OTP+U2F+CCID
				new VendorProductIds(0x2581, 0xf1d0),  // Plug-Up U2F Security Key
				new VendorProductIds(0x2c97, 0x0001),  // Nano S
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
			int sequenceIdx = 0;
			if(apdu == null)
				return null;
			byte[] packet = null;
			var apduStream = new MemoryStream(apdu);
			do
			{
				packet = WrapCommandAPDU(DEFAULT_LEDGER_CHANNEL, apduStream, ref sequenceIdx);
				hid_write(_Device.Handle, packet, packet.Length);
			} while(apduStream.Position != apduStream.Length);

			MemoryStream response = new MemoryStream();
			int remaining = 0;
			sequenceIdx = 0;
			do
			{
				var result = hid_read_timeout(_Device.Handle, packet, MAX_BLOCK, TIMEOUT);
				if(result < 0)
					return null;
				var commandPart = UnwrapReponseAPDU(DEFAULT_LEDGER_CHANNEL, packet, ref sequenceIdx, ref remaining);
				response.Write(commandPart, 0, commandPart.Length);
			} while(remaining != 0);

			return response.ToArray();
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


		byte[] WrapCommandAPDU(uint channel, Stream command, ref int sequenceIdx)
		{
			MemoryStream output = new MemoryStream();
			int position = (int)output.Position;
			output.WriteByte((byte)((channel >> 8) & 0xff));
			output.WriteByte((byte)(channel & 0xff));
			output.WriteByte((byte)TAG_APDU);
			output.WriteByte((byte)((sequenceIdx >> 8) & 0xff));
			output.WriteByte((byte)(sequenceIdx & 0xff));
			if(sequenceIdx == 0)
			{
				output.WriteByte((byte)((command.Length >> 8) & 0xff));
				output.WriteByte((byte)(command.Length & 0xff));
			}
			sequenceIdx++;
			var headerSize = (int)(output.Position - position);
			int blockSize = Math.Min(LEDGER_HID_PACKET_SIZE - headerSize, (int)command.Length - (int)command.Position);

			var commantPart = command.ReadBytes(blockSize);
			output.Write(commantPart, 0, commantPart.Length);
			while((output.Length % LEDGER_HID_PACKET_SIZE) != 0)
				output.WriteByte(0);
			return output.ToArray();
		}

		byte[] UnwrapReponseAPDU(uint channel, byte[] data, ref int sequenceIdx, ref int remaining)
		{
			MemoryStream output = new MemoryStream();
			MemoryStream input = new MemoryStream(data);
			int position = (int)input.Position;
			if(input.ReadByte() != ((channel >> 8) & 0xff))
				return null;
			if(input.ReadByte() != (channel & 0xff))
				return null;
			if(input.ReadByte() != TAG_APDU)
				return null;
			if(input.ReadByte() != ((sequenceIdx >> 8) & 0xff))
				return null;
			if(input.ReadByte() != (sequenceIdx & 0xff))
				return null;

			if(sequenceIdx == 0)
			{
				remaining = ((input.ReadByte()) << 8);
				remaining |= input.ReadByte();
			}
			sequenceIdx++;
			var headerSize = input.Position - position;
			var blockSize = (int)Math.Min(remaining, LEDGER_HID_PACKET_SIZE - headerSize);

			byte[] commandPart = new byte[blockSize];
			if(input.Read(commandPart, 0, commandPart.Length) != commandPart.Length)
				return null;
			output.Write(commandPart, 0, commandPart.Length);
			remaining -= blockSize;
			return output.ToArray();
		}


		private void memcpy(byte[] dest, uint destOffset, byte[] src, uint srcOffset, uint length)
		{
			Array.Copy(src, srcOffset, dest, destOffset, length);
		}

		private void memcpy(Stream dest, byte[] src, uint srcOffset, uint length)
		{
			dest.Write(src, (int)srcOffset, (int)length);
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
