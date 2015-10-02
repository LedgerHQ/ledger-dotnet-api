using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
{
    public class LedgerWalletException : Exception
    {
        public LedgerWalletException(string message)
            : base(message)
        {
        }
        public LedgerWalletException(string message, int sw)
            : this(message, new LedgerWalletStatus(sw))
        {

        }

        public LedgerWalletException(int sw)
            : this(new LedgerWalletStatus(sw))
        {
        }
        public LedgerWalletException(LedgerWalletStatus status)
            : this(status.GetMessage(), status)
        {

        }

        public LedgerWalletException(string message, LedgerWalletStatus status)
            : base(message)
        {
            _Status = status;
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

        public int InternalSW
        {
            get
            {
                if((_SW & 0xFF00) == 0x6F00)
                    return _SW & 0x00FF;
                return 0;
            }
        }

        internal string GetMessage()
        {
            switch(SW)
            {
                case 0x6700:
                    return "Incorrect length";
                case 0x6982:
                    return "Command not allowed : Security status not satisfied";
                case 0x6985:
                    return "Command not allowed : Conditions of use not satisfied";
                case 0x6A80:
                    return "Invalid data";
                case 0x6482:
                    return "File not found";
                case 0x6B00:
                    return "Incorrect parameter P1 or P2";
                case 0x9000:
                    return "OK";
                default:
                    {
                        if((SW & 0xFF00) != 0x6F00)
                            return "Unknown error";
                        switch(InternalSW)
                        {
                            case 0xAA:
                                return "The dongle must be reinserted";
                            default:
                                return "Unknown error";
                        }
                    }
            }
            return "Unknown error";
        }
    }
}
