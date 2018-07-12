using HidLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public class UsageSpecification
	{
		public UsageSpecification(ushort usagePage, ushort usage)
		{
			UsagePage = usagePage;
			Usage = usage;
		}

		public ushort Usage
		{
			get;
			private set;
		}
		public ushort UsagePage
		{
			get;
			private set;
		}
	}
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
					fi = new FileInfo(myass.CodeBase.Replace("file:///", ""));
					moduleHandle = LoadLibraryEx(fi.Directory.FullName + "\\" + folder + "\\hidapi.dll", IntPtr.Zero, 0);
				}
			}
		}

		protected HIDTransportBase(HidDevice device, UsageSpecification[] acceptedUsageSpecifications)
		{
			if(!device.IsOpen)
				device.OpenDevice();
			_Device = device;
			_DevicePath = device.DevicePath;
			_VendorProductIds = new VendorProductIds(device.Attributes.VendorId, device.Attributes.ProductId);
			_AcceptedUsageSpecifications = acceptedUsageSpecifications;
			ReadTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT);
		}

		UsageSpecification[] _AcceptedUsageSpecifications;

		bool needInit = true;
		public string DevicePath
		{
			get
			{
				return _DevicePath;
			}
		}

		protected SemaphoreSlim _SemaphoreSlim = new SemaphoreSlim(1, 1)
;
		bool initializing = false;
		public async Task<byte[][]> ExchangeAsync(byte[][] apdus)
		{
			if(needInit && !initializing)
			{
				initializing = true;
				await InitAsync();
				needInit = false;
				initializing = false;
			}
			var response = await ExchangeCoreAsync(apdus).ConfigureAwait(false);
			if(response == null)
			{
				if(!await RenewTransportAsync())
				{
					throw new LedgerWalletException("Ledger disconnected");
				}
				response = await ExchangeCoreAsync(apdus).ConfigureAwait(false);
				if(response == null)
					throw new LedgerWalletException("Error while transmission");
			}
			return response;
		}

		async Task<bool> RenewTransportAsync()
		{
			var newDevice = EnumerateHIDDevices(new[]
			{
				this._VendorProductIds
			}, _AcceptedUsageSpecifications)
			.FirstOrDefault(hid => hid.DevicePath == _DevicePath);
			if(newDevice == null)
				return false;
			_Device = newDevice;
			if(!_Device.IsOpen)
				_Device.OpenDevice();
			await InitAsync();
			return true;
		}

		protected async virtual Task InitAsync()
		{
		}

		internal static unsafe IEnumerable<HidDevice> EnumerateHIDDevices(IEnumerable<VendorProductIds> vendorProductIds, params UsageSpecification[] acceptedUsages)
		{
			List<HidDevice> devices = new List<HidDevice>();
			foreach(var ids in vendorProductIds)
			{
				if(ids.ProductId == null)
					devices.AddRange(HidDevices.Enumerate(ids.VendorId));
				else
					devices.AddRange(HidDevices.Enumerate(ids.VendorId, ids.ProductId.Value));

			}
			return devices
				.Where(d =>
				acceptedUsages == null ||
				acceptedUsages.Length == 0 ||
				acceptedUsages.Any(u => (ushort)d.Capabilities.UsagePage == u.UsagePage && (ushort)d.Capabilities.Usage == u.Usage));
		}


		const uint MAX_BLOCK = 64;
		const int DEFAULT_TIMEOUT = 20000;

		internal async Task<byte[][]> ExchangeCoreAsync(byte[][] apdus)
		{
			if(apdus == null || apdus.Length == 0)
				return null;
			List<byte[]> resultList = new List<byte[]>();
			var lastAPDU = apdus.Last();

			await _SemaphoreSlim.WaitAsync();

			try
			{
				foreach(var apdu in apdus)
				{
					await WriteAsync(apdu);
					var result = await ReadAsync();
					if(result == null)
						return null;
					resultList.Add(result);
				}
			}
			finally
			{
				_SemaphoreSlim.Release();
			}

			return resultList.ToArray();
		}

		protected async Task<byte[]> ReadAsync()
		{
			byte[] packet = new byte[MAX_BLOCK];
			MemoryStream response = new MemoryStream();
			int remaining = 0;
			int sequenceIdx = 0;
			do
			{
				var result = await hid_read_timeout(_Device.Handle, packet, MAX_BLOCK);
				if(result < 0)
					return null;
				var commandPart = UnwrapReponseAPDU(packet, ref sequenceIdx, ref remaining);
				if(commandPart == null)
					return null;
				response.Write(commandPart, 0, commandPart.Length);
			} while(remaining != 0);

			return response.ToArray();
		}

		protected async Task<byte[]> WriteAsync(byte[] apdu)
		{
			int sequenceIdx = 0;
			byte[] packet = null;
			var apduStream = new MemoryStream(apdu);
			do
			{
				packet = WrapCommandAPDU(apduStream, ref sequenceIdx);
				await hid_write(_Device.Handle, packet, packet.Length);
			} while(apduStream.Position != apduStream.Length);
			return packet;
		}

		protected abstract byte[] UnwrapReponseAPDU(byte[] packet, ref int sequenceIdx, ref int remaining);

		protected abstract byte[] WrapCommandAPDU(Stream apduStream, ref int sequenceIdx);


		protected TimeSpan ReadTimeout
		{
			get; set;
		}

		private async Task<int> hid_read_timeout(IntPtr intPtr, byte[] buffer, uint offset, uint length)
		{
			var bytes = new byte[length];
			Array.Copy(buffer, offset, bytes, 0, length);
			var result = await hid_read_timeout(intPtr, bytes, (uint)length);
			Array.Copy(bytes, 0, buffer, offset, length);
			return result;
		}



		internal async Task<int> hid_read_timeout(IntPtr hidDeviceObject, byte[] buffer, uint length)
		{
			var result = this._Device.Read((int)ReadTimeout.TotalMilliseconds);
			if(result.Status == HidDeviceData.ReadStatus.Success)
			{
				if(result.Data.Length - 1 > length)
					return -1;
				Array.Copy(result.Data, 1, buffer, 0, length);
				return result.Data.Length;
			}
			return -1;
		}

		internal async Task<int> hid_write(IntPtr hidDeviceObject, byte[] buffer, int length)
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
