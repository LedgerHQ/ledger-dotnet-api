using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
{
	public enum WellKnownSW : int
	{
		IncorrectLength = 0x6700,
		SecurityStatusNotSatisfied = 0x6982,
		ConditionsOfUseNotSatisfied = 0x6985,
		InvalidData = 0x6A80,
		FileNotFound = 0x6482,
		IncorrectParameter = 0x6B00,
		OK = 0x9000,
		UnsupportedCommand = 0x6D00
	}
	public class LedgerWalletException : Exception
	{
		public LedgerWalletException(string message) : base(message)
		{

		}
		public LedgerWalletException(string message, LedgerWalletStatus sw)
			: base(message)
		{
			if(sw == null)
				throw new ArgumentNullException("sw");
			_Status = sw;
		}

		private readonly LedgerWalletStatus _Status;
		public LedgerWalletStatus Status
		{
			get
			{
				return _Status;
			}
		}
	}

	public class LedgerWalletStatus
	{
		public LedgerWalletStatus(int sw)
		{
			_SW = sw;
		}
		private readonly int _SW;
		public int SW
		{
			get
			{
				return _SW;
			}
		}

		public WellKnownSW KnownSW
		{
			get
			{
				return (WellKnownSW)SW;
			}
		}

		public int InternalSW
		{
			get
			{
				if((_SW & 0xFF00) == 0x6F00)
					return _SW & 0x00FF;
				return 0;
			}
		}
	}
}
