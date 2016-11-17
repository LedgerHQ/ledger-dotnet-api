using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public class DisposableLock
	{
		class ReleaseLockDisposable : IDisposable
		{
			object l;
			public ReleaseLockDisposable(object l)
			{
				this.l = l;
				Monitor.Enter(l);
			}
			public void Dispose()
			{
				Monitor.Exit(l);
			}
		}
		object l = new object();
		public IDisposable Lock()
		{
			return new ReleaseLockDisposable(l);
		}
	}
}
