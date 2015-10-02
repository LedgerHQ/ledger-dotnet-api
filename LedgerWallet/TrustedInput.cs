using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace LedgerWallet
{
    public class TrustedInput
    {

        public TrustedInput(byte[] response)
        {
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

            if(stream.Serializing)
            {
                uint256 txId = OutPoint.Hash;
                stream.ReadWrite(ref txId);
                uint index = OutPoint.N;
                stream.ReadWrite(ref index);
            }
            else
            {
                uint256 txId = new uint256();
                stream.ReadWrite(ref txId);
                uint index = 0;
                stream.ReadWrite(ref index);
                _OutPoint = new OutPoint(txId, index);
            }

            ulong amount = stream.Serializing ? (ulong)_Amount.Satoshi : 0;
            stream.ReadWrite(ref amount);
            _Amount = Money.Satoshis(amount);

            _Signature = stream.Serializing ? _Signature : new byte[8];
            stream.ReadWrite(ref _Signature);
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

        private OutPoint _OutPoint;
        public OutPoint OutPoint
        {
            get
            {
                return _OutPoint;
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

        public byte[] ToBytes()
        {
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true);
            ReadWrite(stream);
            return ms.ToArray();
        }
    }
}
