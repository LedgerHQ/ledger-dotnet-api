using Android.Content;
using Android.Hardware.Usb;
using Hid.Net;
using Hid.Net.Android;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class AndroidHIDNetProvider : IHIDProvider
    {
        public UsbManager UsbManager { get; private set; }
        public Context AndroidContext { get; private set; }
        public int TimeoutMilliseconds { get; private set; }
        public int ReadBufferLength { get; private set; }

        public AndroidHIDNetProvider(UsbManager usbManager, Context androidContext, int timeoutMilliseconds, int readBufferLength)
        {
            UsbManager = usbManager;
            AndroidContext = androidContext;
            TimeoutMilliseconds = timeoutMilliseconds;
            ReadBufferLength = readBufferLength;
        }

        public IHIDDevice CreateFromDescription(HIDDeviceInformation hidDevice)
        {
            var ledgerUsbDevice = UsbManager.DeviceList.Values.FirstOrDefault(d => d.VendorId == hidDevice.VendorId && d.ProductId == hidDevice.ProductId);
            if (ledgerUsbDevice == null)
            {
                return null;
            }

            var androidHidDevice = new AndroidHidDevice(UsbManager, AndroidContext, TimeoutMilliseconds, ReadBufferLength, ledgerUsbDevice);

            return new AndroidHIDNetDevice(androidHidDevice);
        }

        public Task<IEnumerable<HIDDeviceInformation>> EnumerateDeviceDescriptions(IEnumerable<VendorProductIds> vendorProductIds, UsageSpecification [] acceptedUsages)
        {
            List<DeviceInformation> devices = new List<DeviceInformation>();

            var collection = WindowsHidDevice.GetConnectedDeviceInformations();

            foreach (var ids in vendorProductIds)
            {
                if (ids.ProductId == null)
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId));
                else
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId && c.ProductId == ids.ProductId));

            }
            var retVal = devices
                .Where(d =>
                acceptedUsages == null ||
                acceptedUsages.Length == 0 ||
                acceptedUsages.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage)).ToList();

            return Task.FromResult<IEnumerable<HIDDeviceInformation>>(retVal.Select(r => new HIDDeviceInformation()
            {
                ProductId = r.ProductId,
                VendorId = r.VendorId,
                DevicePath = r.DevicePath,
                ProviderInformation = r
            }));
        }
    }
}
