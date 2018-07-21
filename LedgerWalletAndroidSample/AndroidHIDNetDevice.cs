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
            throw new NotImplementedException();
        }

        public override Task EnsureInitializedAsync(CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }
    }
}
