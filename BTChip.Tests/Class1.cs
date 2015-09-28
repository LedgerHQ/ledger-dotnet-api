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
            var ledger = GetLedger();
            Assert.NotNull(ledger);
            var firmware = ledger.GetFirmwareVersion();
            Assert.NotNull(firmware);
            Assert.True(firmware.ToString().Contains("Loader"));

            Assert.True(ledger.VerifyPin("1234"));
        }

        [Fact]
        public void CanUntrustedSign()
        {

        }

        [Fact]
        [Trait("Manual", "Manual")]
        public void CanRegularSetup()
        {
            var ledger = GetLedger();
            var response = ledger.RegularSetup(new RegularSetup()
            {
                OperationMode = OperationMode.Developer,
                DongleFeatures = DongleFeatures.EnableAllSigHash | DongleFeatures.RFC6979 | DongleFeatures.SkipSecondFactor,
                UserPin = new UserPin("1234")
            });
            Assert.Equal(16, response.TrustedInputKey.Length);
            Assert.Equal(16, response.KeyWrappingKey.Length);
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
            ledger.SetOperationMode(OperationMode.Developer);
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
