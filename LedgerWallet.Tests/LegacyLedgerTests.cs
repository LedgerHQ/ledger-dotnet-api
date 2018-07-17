using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;


namespace LedgerWallet.Tests
{
	[Trait("Legacy", "Legacy")]
	public class LegacyLedgerTests
	{

		[Fact]
		public async Task TestDongleCall()
		{
			//Assume SetServerMode ran before
			var ledger = GetLedger();
			Assert.NotNull(ledger);
			var firmware = await ledger.GetFirmwareVersionAsync();
			Assert.NotNull(firmware);
			Assert.Contains(firmware.ToString(), "Loader");
			Assert.True((await ledger.VerifyPinAsync("1234")).IsSuccess);

			var walletPubKey = await ledger.GetWalletPubKeyAsync(new KeyPath("1'/0"));
			Assert.Equal("1PcLMBsvjkqvs9MaENqHNBpa91atjm89Lb", walletPubKey.Address.ToString());
			Assert.Equal("1PcLMBsvjkqvs9MaENqHNBpa91atjm89Lb", walletPubKey.UncompressedPublicKey.Compress().Hash.GetAddress(Network.Main).ToString());
			Assert.Equal("1PcLMBsvjkqvs9MaENqHNBpa91atjm89Lb", new ExtKey(GetSeed()).Neuter().Derive(1).PubKey.Hash.GetAddress(Network.Main).ToString());
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task SetServerMode()
		{
			//Assume is resetted
			var ledger = GetLedger();
			var seed = GetSeed();
			var response = await ledger.RegularSetupAsync(new RegularSetup()
			{
				OperationMode = OperationMode.Server,
				DongleFeatures = DongleFeatures.EnableAllSigHash | DongleFeatures.RFC6979 | DongleFeatures.SkipSecondFactor,
				UserPin = new UserPin("1234"),
				RestoredSeed = seed,
			});
		}


		private static byte[] GetSeed()
		{
			return Encoders.Hex.DecodeData("1c241d6e8e26990c8b913191d4c1b6cf5d42a63bbd5bffdd90dea34f34ff5a334542db021ae621c0f16cfc39c70e1c23ccbede464851cd5ceaf67266b151f0c2");
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanRegularDeveloperSetup()
		{
			//Assume is resetted
			var ledger = GetLedger();
			var response = await ledger.RegularSetupAsync(new RegularSetup()
			{
				OperationMode = OperationMode.Developer,
				DongleFeatures = DongleFeatures.EnableAllSigHash | DongleFeatures.RFC6979 | DongleFeatures.SkipSecondFactor,
				UserPin = new UserPin("1234"),
				RestoredWrappingKey = new Ledger3DESKey("d16dcd194675a2c96e8915c4b86bebf5")
			});
			Assert.NotNull(response.TrustedInputKey);
			Assert.NotNull(response.WrappingKey);
			Assert.Equal("d16dcd194675a2c96e8915c4b86bebf5", response.WrappingKey.ToHex());
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanGetAndSetOperation()
		{
			//Assume is setup
			var ledger = GetLedger();
			var op = await ledger.GetOperationModeAsync();
			var fact = await ledger.GetSecondFactorModeAsync();
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task ResetLedger()
		{
			for(int i = 0; i < 3; i++)
			{
				var ledger = GetLedger();
				await ledger.VerifyPinAsync("1121");
				Debugger.Break(); //Unplug and replug ledger
			}
		}

		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanSeeRemainingTries()
		{
			//Assume is setup
			var ledger = GetLedger();
			var verifyPinResult = await ledger.VerifyPinAsync("1234");
			Assert.True(verifyPinResult.IsSuccess);

			verifyPinResult.Remaining = await ledger.GetRemainingAttemptsAsync();
			Assert.Equal(3, verifyPinResult.Remaining);
			var result = await ledger.VerifyPinAsync("1235");
			Assert.Equal(2, verifyPinResult.Remaining);

			var ex = await Assert.ThrowsAsync<LedgerWalletException>(async () => verifyPinResult.Remaining = await ledger.GetRemainingAttemptsAsync());
			Assert.NotNull(ex.Status);
			Assert.True(ex.Status.SW == 0x6FAA);
			Assert.True(ex.Status.InternalSW == 0x00AA);

			Debugger.Break(); //Remove then insert

			ledger = (LegacyLedgerClient)await NanoSTests.GetLedgerAsync(NanoSTests.LedgerType.LegacyLedger);
			verifyPinResult.Remaining = await ledger.GetRemainingAttemptsAsync();
			Assert.Equal(2, verifyPinResult.Remaining);
		}

		private static LegacyLedgerClient GetLedger()
		{
			return (LegacyLedgerClient)NanoSTests.GetLedgerAsync(NanoSTests.LedgerType.LegacyLedger).GetAwaiter().GetResult();
		}
	}
}