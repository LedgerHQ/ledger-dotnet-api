# Ledger Wallet Client API

<img src="http://segwit.co/static/public/images/logo.png" width="100"> [![NuGet](https://img.shields.io/nuget/v/NBitcoin.svg)](https://www.nuget.org/packages/LedgerWallet/)

The is a .NET library to use build application using the ledger Nano S.

* Use the `LedgerClient` class for the Ledger Bitcoin App.
* Use the `U2FClient` class for the U2F Ledger App.
* Use a custom transport protocol to talk to your ledger (Example: having a HTTP proxy to talk to a remote ledger connected to your server)
* You can easily build your own client for your custom App.
* Support Segwit

Support only Windows for the time being. Supporting other plateform is theorically possible if you can compile hidapi library by yourself.

## How to use ?

Reference the [nuget package](https://www.nuget.org/packages/LedgerWallet/) in your project.

Then you can easily sign:

```
var ledger = LedgerClient.GetHIDLedgers().First();

var walletPubKey = ledger.GetWalletPubKey(new KeyPath("1'/0"));
var address1 = walletPubKey.Address.ScriptPubKey;
var walletPubKey2 = ledger.GetWalletPubKey(new KeyPath("1'/0"));
var address2 =walletPubKey2.Address.ScriptPubKey;

var changeAddress = (BitcoinAddress)ledger.GetWalletPubKey(new KeyPath("1'/1")).Address;

Transaction funding = new Transaction();
funding.AddInput(Network.Main.GetGenesis().Transactions[0].Inputs[0]);
funding.Outputs.Add(new TxOut(Money.Coins(1.1m), address1));
funding.Outputs.Add(new TxOut(Money.Coins(1.0m), address1));
funding.Outputs.Add(new TxOut(Money.Coins(1.2m), address2));

var coins = funding.Outputs.AsCoins();

var spending = new Transaction();
spending.LockTime = 1;
spending.Inputs.AddRange(coins.Select(o => new TxIn(o.Outpoint, Script.Empty)));
spending.Inputs[0].Sequence = 1;
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

//should show 0.5 and 2.0 btc in fee
var signed = ledger.SignTransaction(requests, spending, new KeyPath("1'/1"));
//Assert.Equal(Script.Empty, spending.Inputs.Last().ScriptSig);
Assert.NotNull(signed);
foreach(var req in requests)
{
    Assert.NotNull(req.Signature);
}
```

You can check the tests [NanoSTests](https://github.com/LedgerHQ/ledger-dotnet-api/blob/master/LedgerWallet.Tests/NanoSTests.cs) and [U2FTests](https://github.com/LedgerHQ/ledger-dotnet-api/blob/master/LedgerWallet.Tests/U2FTests.cs) for additional informations.
