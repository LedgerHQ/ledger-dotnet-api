using Hid.Net;
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
		internal IHidDevice _Device;
#if(!NETSTANDARD2_0)
		readonly string _DevicePath;
#endif
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

		protected HIDTransportBase(IHidDevice device, UsageSpecification[] acceptedUsageSpecifications)
		{
#if(!NETSTANDARD2_0)
			var windowsHidDevice = device as WindowsHidDevice;
			if(!windowsHidDevice.IsInitialized)
				windowsHidDevice.Initialize();

			_DevicePath = ((WindowsHidDevice)device).DevicePath;
#endif
			_Device = device;

			_VendorProductIds = new VendorProductIds(device.VendorId, device.ProductId);
			_AcceptedUsageSpecifications = acceptedUsageSpecifications;
			ReadTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT);
		}

		UsageSpecification[] _AcceptedUsageSpecifications;

		bool needInit = true;
#if(!NETSTANDARD2_0)
		public string DevicePath
		{
			get
			{
				return _DevicePath;
			}
		}
#endif

		protected SemaphoreSlim _SemaphoreSlim = new SemaphoreSlim(1, 1)
;
		bool initializing = false;
		public async Task<byte[][]> Exchange(byte[][] apdus)
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
#if(!NETSTANDARD2_0)
				if(!await RenewTransportAsync())
				{
					throw new LedgerWalletException("Ledger disconnected");
				}
#endif

				response = await ExchangeCoreAsync(apdus).ConfigureAwait(false);
				if(response == null)
					throw new LedgerWalletException("Error while transmission");
			}

			return response;
		}

#if(!NETSTANDARD2_0)
		async Task<bool> RenewTransportAsync()
		{
			var newDevice = EnumerateHIDDevices(new[]
			{
				this._VendorProductIds
			}, _AcceptedUsageSpecifications)
			.FirstOrDefault(hid => hid.DevicePath == _DevicePath);
			if(newDevice == null)
				return false;
			_Device = new WindowsHidDevice(newDevice);
			if(!await _Device.GetIsConnectedAsync())
			{
				var windowsHidDevice = _Device as WindowsHidDevice;
				windowsHidDevice.Initialize();
			}
			await InitAsync();
			return true;
		}
#endif

		protected async virtual Task InitAsync()
		{
		}

#if(!NETSTANDARD2_0)
		internal static unsafe IEnumerable<DeviceInformation> EnumerateHIDDevices(IEnumerable<VendorProductIds> vendorProductIds, params UsageSpecification[] acceptedUsages)
		{
			List<DeviceInformation> devices = new List<DeviceInformation>();

			var collection = WindowsHidDevice.GetConnectedDeviceInformations();
			foreach(var vendorProductId in vendorProductIds)
			{
				devices.AddRange(collection.Where(d => d.VendorId == vendorProductId.VendorId && d.ProductId == vendorProductId.ProductId));
			}

			return devices
				.Where(d =>
				acceptedUsages == null ||
				acceptedUsages.Length == 0 ||
				acceptedUsages.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage));
		}
#endif

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
				var result = await hid_read_timeout(packet, MAX_BLOCK);
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
				await hid_write(packet, packet.Length);
			} while(apduStream.Position != apduStream.Length);
			return packet;
		}

		protected abstract byte[] UnwrapReponseAPDU(byte[] packet, ref int sequenceIdx, ref int remaining);

		protected abstract byte[] WrapCommandAPDU(Stream apduStream, ref int sequenceIdx);


		protected TimeSpan ReadTimeout
		{
			get; set;
		}

		private async Task<int> hid_read_timeout(byte[] buffer, uint offset, uint length)
		{
			var bytes = new byte[length];
			Array.Copy(buffer, offset, bytes, 0, length);
			var result = await hid_read_timeout(bytes, length);
			Array.Copy(bytes, 0, buffer, offset, length);
			return result;
		}

		internal async Task<int> hid_read_timeout(byte[] buffer, uint length)
		{
			//Note: this method used to read 64 bytes and shift that right in to an array of 65 bytes
			//Android does this automatically, so, we can't do this here for compatibility with Android.
			//See the flag DataHasExtraByte on WindowsHidDevice and UWPHidDevice

			var result = await _Device.ReadAsync();
			Array.Copy(result, 0, buffer, 0, length);
			return result.Length;
		}

		internal async Task<int> hid_write(byte[] buffer, int length)
		{
			//Note: this method used to take 64 bytes and shift that right in to an array of 65 bytes and then write to the device
			//Android does this automatically, so, we can't do this here for compatibility with Android.
			//See the flag DataHasExtraByte on WindowsHidDevice and UWPHidDevice

			await _Device.WriteAsync(buffer);
			return length;
		}
	}
}
