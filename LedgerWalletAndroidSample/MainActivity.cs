using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Hid.Net.Android;
using LedgerWallet;
using LedgerWallet.HIDProviders.HIDNet;
using LedgerWallet.Transports;
using System;
using System.Threading.Tasks;

namespace LedgerWalletAndroidSample
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    [IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached })]
    [MetaData(UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
    public class MainActivity : AppCompatActivity
    {
        private AndroidHidDevice _LedgerHidDevice;
        private UsbDeviceAttachedReceiver _UsbDeviceAttachedReceiver;
        private UsbDeviceDetachedReceiver _UsbDeviceDetachedReceiver;
        private object _ReceiverLock = new object();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            _LedgerHidDevice = new AndroidHidDevice(GetSystemService(UsbService) as UsbManager, ApplicationContext, 3000, 64, 11415, 1);

            _LedgerHidDevice.Connected += _LedgerHidDevice_Connected;

            RegisterReceiver();

            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;
        }

        private async void _LedgerHidDevice_Connected(object sender, EventArgs e)
        {
            try
            {
                await Task.Delay(1000);
                var androidHIDNetDevice = new AndroidHIDNetDevice(_LedgerHidDevice);
                var ledgerTransport = new HIDLedgerTransport(androidHIDNetDevice);
                var ledgerClient = new LedgerClient(ledgerTransport);
                var firmwareVersion = await ledgerClient.GetFirmwareVersionAsync();
                var formattedVersion = $"Firmware Version: {firmwareVersion.Major}.{firmwareVersion.Minor}.{firmwareVersion.Patch}";
                FindViewById<TextView>(Resource.Id.TheTextView).Text = formattedVersion;
            }
            catch (Exception ex)
            {
                Toast.MakeText(ApplicationContext, ex.ToString(), ToastLength.Long).Show();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View)sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }

        protected override void OnResume()
        {
            base.OnResume();


            RegisterReceiver();
        }

        private void RegisterReceiver()
        {
            try
            {
                lock (_ReceiverLock)
                {
                    if (_UsbDeviceAttachedReceiver != null)
                    {
                        UnregisterReceiver(_UsbDeviceAttachedReceiver);
                        _UsbDeviceAttachedReceiver.Dispose();
                    }

                    _UsbDeviceAttachedReceiver = new UsbDeviceAttachedReceiver(_LedgerHidDevice);
                    RegisterReceiver(_UsbDeviceAttachedReceiver, new IntentFilter(UsbManager.ActionUsbDeviceAttached));

                    if (_UsbDeviceDetachedReceiver != null)
                    {
                        UnregisterReceiver(_UsbDeviceDetachedReceiver);
                        _UsbDeviceDetachedReceiver.Dispose();
                    }

                    _UsbDeviceDetachedReceiver = new UsbDeviceDetachedReceiver(_LedgerHidDevice);
                    RegisterReceiver(_UsbDeviceDetachedReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));
                }
            }
            catch (Exception ex)
            {
            }
        }

    }
}

