using LedgerWallet.Transports;
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
		public void LedgerIsThreadSafe()
		{
			var ledger = GetLedger();
			List<Task> tasks = new List<Task>();
			bool exception = false;

			Thread t1 = new Thread(() =>
			{
				try
				{
					for(int i = 0; i < 50; i++)
					{
						GetLedger().GetFirmwareVersion();
					}
				}
				catch
				{
					exception = true;
				}
			});
			t1.Start();

			Thread t2 = new Thread(() =>
			{
				try
				{

					for(int i = 0; i < 50; i++)
					{
						ledger.GetWalletPubKey(new KeyPath("1'/0"));
					}
				}
				catch
				{
					exception = true;
				}
			});
			t2.Start();

			t1.Join();
			t2.Join();
			Assert.False(exception);
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public void CanSignTransactionStandardMode()
		{
			CanSignTransactionStandardModeCore(true);
			CanSignTransactionStandardModeCore(false);
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public void CanGetWalletPubKey()
		{
			var ledger = GetLedger();
			ledger.GetFirmwareVersion();
			var path = new KeyPath("1'/0");
			ledger.GetWalletPubKey(path, LedgerClient.AddressType.Legacy, true);
			ledger.GetWalletPubKey(path, LedgerClient.AddressType.NativeSegwit, false);
			ledger.GetWalletPubKey(path, LedgerClient.AddressType.Segwit, false);
		}

		public void CanSignTransactionStandardModeCore(bool segwit)
		{
			var ledger = GetLedger();

			var walletPubKey = ledger.GetWalletPubKey(new KeyPath("1'/0"));
			var address = segwit ? walletPubKey.UncompressedPublicKey.Compress().WitHash.ScriptPubKey : walletPubKey.Address.ScriptPubKey;

			var changeAddress = (BitcoinAddress)ledger.GetWalletPubKey(new KeyPath("1'/1")).Address;

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
			//spending.Inputs.Add(new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty));
			spending.Outputs.Add(new TxOut(Money.Coins(0.5m), BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")));
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
			var signed = ledger.SignTransaction(requests, spending, new KeyPath("1'/1"));
			//Assert.Equal(Script.Empty, spending.Inputs.Last().ScriptSig);
			Assert.NotNull(signed);
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public void CanSignTransactionStandardModeConcurrently()
		{
			var ledger = GetLedger();

			var walletPubKey = ledger.GetWalletPubKey(new KeyPath("1'/0"));
			var address = (BitcoinAddress)walletPubKey.Address;

			var changeAddress = (BitcoinAddress)ledger.GetWalletPubKey(new KeyPath("1'/1")).Address;

			Transaction funding = new Transaction();
			funding.AddInput(Network.Main.GetGenesis().Transactions[0].Inputs[0]);
			funding.Outputs.Add(new TxOut(Money.Coins(1.1m), address));
			funding.Outputs.Add(new TxOut(Money.Coins(1.0m), address));
			funding.Outputs.Add(new TxOut(Money.Coins(1.2m), address));

			var coins = funding.Outputs.AsCoins();

			var spending = new Transaction();
			spending.LockTime = 1;
			spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, o.ScriptPubKey)));
			spending.Outputs.Add(new TxOut(Money.Coins(0.5m), BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")));
			spending.Outputs.Add(new TxOut(Money.Coins(0.8m), changeAddress));
			spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));

			Parallel.For(0, 5, i =>
			{
				//should show 0.5 and 2.0 btc in fee
				var signed = ledger.SignTransaction(
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
			});
		}

		private static LedgerClient GetLedger()
		{
			return LedgerClient.GetHIDLedgers().First();
		}
	}
}
