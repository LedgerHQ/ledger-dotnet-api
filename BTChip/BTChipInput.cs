using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace BTChip
{
    public class BTChipInput
    {

        public BTChipInput(byte[] response, bool trusted)
        {
            _Trusted = trusted;
            var stream = new BitcoinStream(new MemoryStream(response), false);
            ReadWrite(stream);
        }

        private void ReadWrite(BitcoinStream stream)
        {
            if(stream.Serializing)
            {
                stream.ReadWrite((byte)0x32);
                stream.ReadWrite(Flags);
            }
            else
            {
                if(stream.Inner.ReadByte() != 0x32)
                    throw new FormatException("Invalid magic version");
                Flags = (byte)stream.Inner.ReadByte();
            }
            stream.ReadWrite(ref _Nonce);
            stream.ReadWrite(ref _TransactionId);
            stream.ReadWrite(ref _Index);

            ulong amount = stream.Serializing ? (ulong)_Amount.Satoshi : 0;
            stream.ReadWrite(ref amount);
            _Amount = Money.Satoshis(amount);

            _Signature = stream.Serializing ? _Signature : new byte[8];
            stream.ReadWrite(ref _Signature);
        }

        private bool _Trusted;
        public bool Trusted
        {
            get
            {
                return _Trusted;
            }
        }

        public byte Flags
        {
            get;
            internal set;
        }

        private short _Nonce;
        public short Nonce
        {
            get
            {
                return _Nonce;
            }
        }

        private uint256 _TransactionId;
        public uint256 TransactionId
        {
            get
            {
                return _TransactionId;
            }
        }

        private int _Index;
        public int Index
        {
            get
            {
                return _Index;
            }
        }

        private Money _Amount;
        public Money Amount
        {
            get
            {
                return _Amount;
            }
        }

        private byte[] _Signature;
        public byte[] Signature
        {
            get
            {
                return _Signature;
            }
        }
    }
}
