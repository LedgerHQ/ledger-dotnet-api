using Hid.Net;
using LedgerWallet.Transports;
using LedgerWallet.U2F;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace LedgerWallet.Tests
{
	[Trait("NanoS", "NanoS")]
	public class NanoSTests
	{

		[Fact]
		public async Task LedgerIsThreadSafe()
		{
			var ledger = (LedgerClient)await GetLedgerAsync(LedgerType.Ledger);

			var tasks = new List<Task>();

			for(int i = 0; i < 50; i++)
			{
				tasks.Add(ledger.GetWalletPubKeyAsync(new KeyPath("1'/0")));
				tasks.Add(ledger.GetFirmwareVersionAsync());
			}

			await Task.WhenAll(tasks);
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanSignTransactionStandardMode()
		{
			await CanSignTransactionStandardModeCore(true);
			await CanSignTransactionStandardModeCore(false);
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanGetWalletPubKey()
		{
			var ledger = (LedgerClient)await GetLedgerAsync(LedgerType.Ledger);
			var firmwareVersion = ledger.GetFirmwareVersionAsync();
			var path = new KeyPath("1'/0");
			var walletPubKeyResponse = ledger.GetWalletPubKeyAsync(path, LedgerClient.AddressType.Legacy, true);
			await ledger.GetWalletPubKeyAsync(path, LedgerClient.AddressType.NativeSegwit, false);
			await ledger.GetWalletPubKeyAsync(path, LedgerClient.AddressType.Segwit, false);
		}

		public async Task CanSignTransactionStandardModeCore(bool segwit)
		{
			var ledger = (LedgerClient)await GetLedgerAsync(LedgerType.Ledger);
			var walletPubKey = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/0"));
			var address = segwit ? walletPubKey.UncompressedPublicKey.Compress().WitHash.ScriptPubKey : walletPubKey.GetAddress(network).ScriptPubKey;

			var response = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/1"));
			var changeAddress = response.GetAddress(network);

			Transaction funding = new Transaction();
			funding.AddInput(Network.Main.GetGenesis().Transactions[0].Inputs[0]);
			funding.Outputs.Add(new TxOut(Money.Coins(1.1m), address));
			funding.Outputs.Add(new TxOut(Money.Coins(1.0m), address));
			funding.Outputs.Add(new TxOut(Money.Coins(1.2m), address));

			var coins = funding.Outputs.AsCoins();

			var spending = new Transaction();
			spending.LockTime = 1;
			spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, Script.Empty)));
			spending.Inputs[0].Sequence = 1;
			spending.Outputs.Add(new TxOut(Money.Coins(0.5m), BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe", Network.Main)));
			spending.Outputs.Add(new TxOut(Money.Coins(0.8m), changeAddress));
			spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));


			var requests = new SignatureRequest[]{
				new SignatureRequest()
				{
					InputCoin = new Coin(funding, 0),
					InputTransaction = funding,
					KeyPath = new KeyPath("1'/0")
				},
				new SignatureRequest()
				{
					InputCoin = new Coin(funding, 1),
					InputTransaction = funding,
					KeyPath = new KeyPath("1'/0")
				},
				new SignatureRequest()
				{
					InputCoin = new Coin(funding, 2),
					InputTransaction = funding,
					KeyPath = new KeyPath("1'/0")
				},
			};

			if(segwit)
			{
				foreach(var req in requests)
					req.InputTransaction = null;
			}

			//should show 0.5 and 2.0 btc in fee
			var signed = await ledger.SignTransactionAsync(requests, spending, new KeyPath("1'/1"));
			//Assert.Equal(Script.Empty, spending.Inputs.Last().ScriptSig);
			Assert.NotNull(signed);
		}

		Network network = Network.Main;

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanSignTransactionStandardModeConcurrently()
		{
			var ledger = (LedgerClient)await GetLedgerAsync(LedgerType.Ledger);

			var walletPubKey = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/0"));
			var address = walletPubKey.GetAddress(network);

			var walletPubKey2 = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/1"));
			var changeAddress = walletPubKey2.GetAddress(network);

			Transaction funding = new Transaction();
			funding.AddInput(network.GetGenesis().Transactions[0].Inputs[0]);
			funding.Outputs.Add(new TxOut(Money.Coins(1.1m), address));
			funding.Outputs.Add(new TxOut(Money.Coins(1.0m), address));
			funding.Outputs.Add(new TxOut(Money.Coins(1.2m), address));

			var coins = funding.Outputs.AsCoins();

			var spending = new Transaction();
			spending.LockTime = 1;
			spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, o.ScriptPubKey)));
			spending.Outputs.Add(new TxOut(Money.Coins(0.5m), BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe", Network.Main)));
			spending.Outputs.Add(new TxOut(Money.Coins(0.8m), changeAddress));
			spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));

			var tasks = new List<Task>();

			for(var i = 0; i < 5; i++)
			{
				//should show 0.5 and 2.0 btc in fee
				var signed = ledger.SignTransactionAsync(
				  new KeyPath("1'/0"),
				  new Coin[]
				{
				new Coin(funding, 0),
				new Coin(funding, 1),
				new Coin(funding, 2),
				}, new Transaction[]
				{
				funding
				}, spending, new KeyPath("1'/1"));

				tasks.Add(signed);
			}

			await Task.WhenAll(tasks);
		}

#if(!NETSTANDARD2_0)
		public async static Task<LedgerClientBase> GetLedgerAsync(LedgerType ledgerType)
		{
			return LedgerClient.GetHIDLedgers().First();
		}
#else
		public async static Task<LedgerClientBase> GetLedgerAsync(LedgerType ledgerType)
		{
			var vid = (ushort)11415;
			var devices = WindowsHidDevice.GetConnectedDeviceInformations();
			var newDevice = devices.Where(d => d.VendorId == vid).ToList()[0];
			var windowsHidDevice = new WindowsHidDevice(newDevice);
			windowsHidDevice.DataHasExtraByte = true;
			await windowsHidDevice.InitializeAsync();
			var ledgerTransport = new HIDLedgerTransport(windowsHidDevice);

			switch(ledgerType)
			{
				case LedgerType.Ledger:
					return new LedgerClient(ledgerTransport);
				case LedgerType.LegacyLedger:
					return new LegacyLedgerClient(ledgerTransport);
				case LedgerType.U2F:
					return new U2FClient(ledgerTransport);
				default:
					throw new NotImplementedException();
			}
		}
#endif

		public enum LedgerType
		{
			Ledger,
			LegacyLedger,
			U2F
		}
	}
}
