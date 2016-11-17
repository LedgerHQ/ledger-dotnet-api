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
        public void CanSignTransactionStandardMode()
		{
			var ledger = GetLedger();

            var walletPubKey = ledger.GetWalletPubKey(new KeyPath("1'/0"));
            var address = (BitcoinAddress)walletPubKey.Address;

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
            spending.Outputs.Add(new TxOut(Money.Coins(0.8m), address));
            spending.Outputs.Add(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 })));

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
            }, spending);
        }

        private static LedgerClient GetLedger()
        {
            var ledger = LedgerClient.GetHIDLedgers().First();
            return ledger;
        }
    }
}
