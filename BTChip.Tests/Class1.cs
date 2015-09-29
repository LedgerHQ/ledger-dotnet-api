using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BTChip.Tests
{
    public class Class1
    {
        [Fact]
        public void TestDongleCall()
        {
            //Assume SetServerMode ran before
            var ledger = GetLedger();
            Assert.NotNull(ledger);
            var firmware = ledger.GetFirmwareVersion();
            Assert.NotNull(firmware);
            Assert.True(firmware.ToString().Contains("Loader"));
            Assert.True(ledger.VerifyPin("1234"));

            var walletPubKey = ledger.GetWalletPubKey(new KeyPath("1"));
            Assert.Equal("1PcLMBsvjkqvs9MaENqHNBpa91atjm89Lb", walletPubKey.Address.ToString());
            Assert.Equal("1PcLMBsvjkqvs9MaENqHNBpa91atjm89Lb", walletPubKey.UncompressedPublicKey.Compress().Hash.GetAddress(Network.Main).ToString());
            Assert.Equal("1PcLMBsvjkqvs9MaENqHNBpa91atjm89Lb", new ExtKey(GetSeed()).Neuter().Derive(1).PubKey.Hash.GetAddress(Network.Main).ToString());
        }

        private static byte[] GetSeed()
        {
            return Encoders.Hex.DecodeData("1c241d6e8e26990c8b913191d4c1b6cf5d42a63bbd5bffdd90dea34f34ff5a334542db021ae621c0f16cfc39c70e1c23ccbede464851cd5ceaf67266b151f0c2");
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public void SetServerMode()
        {
            var ledger = GetLedger();
            var seed = GetSeed();
            var response = ledger.RegularSetup(new RegularSetup()
            {
                OperationMode = OperationMode.Server,
                DongleFeatures = DongleFeatures.EnableAllSigHash | DongleFeatures.RFC6979 | DongleFeatures.SkipSecondFactor,
                UserPin = new UserPin("1234"),
                RestoredSeed = seed,
            });

        }

        [Fact]
        [Trait("Manual", "Manual")]
        public void CanRegularDeveloperSetup()
        {
            var ledger = GetLedger();
            var response = ledger.RegularSetup(new RegularSetup()
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
        public void CanGetAndSetOperation()
        {
            var ledger = GetLedger();
            var op = ledger.GetOperationMode();
            var fact = ledger.GetSecondFactorMode();
            //ledger.VerifyPin("1111");
            ledger.VerifyPin("1234");
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public void ResetLedger()
        {
            for(int i = 0; i < 3; i++)
            {
                var ledger = GetLedger();
                ledger.VerifyPin("1121");
                Debugger.Break(); //Unplug and replug ledger
            }
        }

        private static LedgerClient GetLedger()
        {
            var ledger = LedgerClient.GetLedgers().FirstOrDefault();
            return ledger;
        }

        [Fact]
        [Trait("Manual", "Manual")]
        public void CanSeeRemainingTries()
        {
            var ledger = GetLedger();
            Assert.True(ledger.VerifyPin("1234"));

            int tries;
            tries = ledger.GetRemainingAttempts();
            Assert.Equal(3, tries);
            ledger.VerifyPin("1235", out tries);
            Assert.Equal(2, tries);

            var ex = Assert.Throws<BTChipException>(() => tries = ledger.GetRemainingAttempts());
            Assert.NotNull(ex.Status);
            Assert.NotNull(ex.Status.SW == 0x6FAA);
            Assert.NotNull(ex.Status.InternalSW == 0x00AA);

            Debugger.Break(); //Remove then insert

            ledger = GetLedger();
            tries = ledger.GetRemainingAttempts();
            Assert.Equal(2, tries);
        }
    }
}
