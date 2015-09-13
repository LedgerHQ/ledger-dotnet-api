using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaco.Ledger
{
    public class BTChipException : Exception
    {
        public BTChipException(string message)
            : base(message)
        {
        }
        public BTChipException(string message, int sw)
            : this(message, new BTChipStatus(sw))
        {

        }

        public BTChipException(int sw)
            : this(new BTChipStatus(sw))
        {
        }
        public BTChipException(BTChipStatus status)
            : this(status.GetMessage(), status)
        {

        }

        public BTChipException(string message, BTChipStatus status)
            : base(message)
        {
            _Status = status;
        }

        private readonly BTChipStatus _Status;
        public BTChipStatus Status
        {
            get
            {
                return _Status;
            }
        }
    }

    public class BTChipStatus
    {
        public BTChipStatus(int sw)
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
