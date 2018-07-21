using Hid.Net.Android;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class AndroidHIDNetDevice : HIDNetDevice
    {
        AndroidHidDevice _AndroidHidDevice;

        public AndroidHIDNetDevice(AndroidHidDevice androidHidDevice) : base(androidHidDevice)
        {
            _AndroidHidDevice = androidHidDevice;
        }

        public override IHIDDevice Clone()
        {
            return new AndroidHIDNetDevice(_AndroidHidDevice);
        }

        public async override Task EnsureInitializedAsync(CancellationToken cancellation)
        {
            try
            {
                var isConnected = !await _AndroidHidDevice.GetIsConnectedAsync();
                if (!isConnected)
                {
                    throw new Exception("There is no Ledger connected to your Android device");
                }
            }
            catch (Exception ex)
            {
                throw new HIDDeviceException($"The following error occurred while attempting to connect to the Ledger.\r\n{ex.Message}", ex);
            }
        }
    }
}
