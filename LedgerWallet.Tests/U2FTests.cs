using LedgerWallet.U2F;
using NBitcoin.DataEncoders;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LedgerWallet.Tests
{
	[Trait("U2F", "U2F")]
	public class U2FTests
	{
		[Fact]
		[Trait("Manual", "Manual")]
		public async Task CanEnrollAndAuthenticate()
		{
			var appId = new AppId(Encoders.Hex.DecodeData("d2e42c173c857991d5e1b6c81f3e07cbb9d5f57431fe41997c9445c14ce61ec4"));
			var challenge = Encoders.Hex.DecodeData("e6425678fbd7d3d8e311fbfb1db8d26c37cf9f16ac81c95848998a76ce3d3768");
			var u2f = (await U2FClient.GetHIDU2FAsync()).First();

			// Refuse registration
			Debugger.Break();
			var cts = new CancellationTokenSource();
			cts.CancelAfter(5000);
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await u2f.RegisterAsync(challenge, appId, cts.Token));

			// Accept registration
			Debugger.Break();
			var reg = await u2f.RegisterAsync(challenge, appId);
			Assert.NotNull(reg);

			// Refuse login
			Debugger.Break();
			cts = new CancellationTokenSource();
			cts.CancelAfter(5000);
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await u2f.AuthenticateAsync(challenge, appId, reg.KeyHandle, cts.Token));

			// Accept registration
			Debugger.Break();
			var login = await u2f.AuthenticateAsync(challenge, appId, reg.KeyHandle);
			Assert.NotNull(login);
		}
	}
}
