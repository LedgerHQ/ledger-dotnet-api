using HidLibrary;
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
	public abstract class HIDTransportBase : ILedgerTransport
	{
		internal HidDevice _Device;
		readonly string _DevicePath;
		readonly VendorProductIds _VendorProductIds;

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, int dwFlags);

		static HIDTransportBase()
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

		protected HIDTransportBase(HidDevice device)
		{
			if(!device.IsOpen)
				device.OpenDevice();
			_Device = device;
			_DevicePath = device.DevicePath;
			_VendorProductIds = new VendorProductIds(device.Attributes.VendorId, device.Attributes.ProductId);
		}

		bool needInit = true;
		public string DevicePath
		{
			get
			{
				return _DevicePath;
			}
		}

		DisposableLock l = new DisposableLock();
		public IDisposable Lock()
		{
			return l.Lock();
		}

		bool initializing = false;
		public byte[] Exchange(byte[] apdu)
		{
			if(needInit && !initializing)
			{
				initializing = true;
				Init();
				needInit = false;
				initializing = false;
			}
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

		bool RenewTransport()
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
			Init();
			return true;
		}

		protected virtual void Init()
		{
		}

		internal static unsafe IEnumerable<HidDevice> EnumerateHIDDevices(IEnumerable<VendorProductIds> vendorProductIds)
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


		const uint MAX_BLOCK = 64;
		const int TIMEOUT = 20000;

		internal byte[] ExchangeCore(byte[] apdu)
		{
			using(this.Lock())
			{
				if(apdu == null)
					return null;
				Write(apdu);
				return Read();
			}
		}

		protected byte[] Read()
		{
			byte[] packet = new byte[MAX_BLOCK];
			MemoryStream response = new MemoryStream();
			int remaining = 0;
			int sequenceIdx = 0;
			do
			{
				var result = hid_read_timeout(_Device.Handle, packet, MAX_BLOCK, TIMEOUT);
				if(result < 0)
					return null;
				var commandPart = UnwrapReponseAPDU(packet, ref sequenceIdx, ref remaining);
				if(commandPart == null)
					return null;
				response.Write(commandPart, 0, commandPart.Length);
			} while(remaining != 0);

			return response.ToArray();
		}

		protected byte[] Write(byte[] apdu)
		{
			int sequenceIdx = 0;
			byte[] packet = null;
			var apduStream = new MemoryStream(apdu);
			do
			{
				packet = WrapCommandAPDU(apduStream, ref sequenceIdx);
				hid_write(_Device.Handle, packet, packet.Length);
			} while(apduStream.Position != apduStream.Length);
			return packet;
		}

		protected abstract byte[] UnwrapReponseAPDU(byte[] packet, ref int sequenceIdx, ref int remaining);

		protected abstract byte[] WrapCommandAPDU(Stream apduStream, ref int sequenceIdx);

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

	}
}
