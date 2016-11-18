using LedgerWallet.U2F;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		public void CanEnrollAndAuthenticate()
		{
			var appId = new AppId(Encoders.Hex.DecodeData("d2e42c173c857991d5e1b6c81f3e07cbb9d5f57431fe41997c9445c14ce61ec4"));
			var challenge = Encoders.Hex.DecodeData("e6425678fbd7d3d8e311fbfb1db8d26c37cf9f16ac81c95848998a76ce3d3768");
			U2FClient u2f = U2FClient.GetHIDU2F().First();
			CancellationTokenSource cts = new CancellationTokenSource();
			cts.CancelAfter(5000);
			Assert.Throws<OperationCanceledException>(() => u2f.Register(challenge, appId, cts.Token)); // refuse registration
			var reg = u2f.Register(challenge, appId); // accept registration
			Assert.NotNull(reg);
		}
	}
}
