using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaco.Ledger
{
    public static class Ext
    {
        public static void write(this Stream stream, int value)
        {
            var bytes = new byte[]
				{
					(byte)(value >> 24),
					(byte)(value >> 16),
					(byte)(value >> 8),
					(byte)value,
				};
            stream.Write(bytes, 0, bytes.Length);
        }
    }
    internal class BTChipTransport
    {
        SafeFileHandle _Handle;
        int TAG_APDU = 0x05;
        public BTChipTransport(SafeFileHandle handle)
        {
            _Handle = handle;
        }

        internal byte[] Exchange(byte[] command)
        {
            MemoryStream response = new MemoryStream();
            byte[] responseData = null;
            int offset = 0;
            int responseSize;
            int result;

            command = WrapCommandAPDU(LEDGER_DEFAULT_CHANNEL, command, HID_BUFFER_SIZE);

            return null;
        }

        private byte[] WrapCommandAPDU(int channel, byte[] command, int packetSize)
        {
            MemoryStream output = new MemoryStream();
            if(packetSize < 3)
            {
                throw new BTChipException("Can't handle Ledger framing with less than 3 bytes for the report");
            }
            int sequenceIdx = 0;
            int offset = 0;
            output.write(channel >> 8);
            output.write(channel);
            output.write(TAG_APDU);
            output.write(sequenceIdx >> 8);
            output.write(sequenceIdx);
            sequenceIdx++;
            output.write(command.Length >> 8);
            output.write(command.Length);
            int blockSize = (command.Length > packetSize - 7 ? packetSize - 7 : command.Length);
            output.Write(command, offset, blockSize);
            offset += blockSize;
            while(offset != command.Length)
            {
                output.write(channel >> 8);
                output.write(channel);
                output.write(TAG_APDU);
                output.write(sequenceIdx >> 8);
                output.write(sequenceIdx);
                sequenceIdx++;
                blockSize = (command.Length - offset > packetSize - 5 ? packetSize - 5 : command.Length - offset);
                output.Write(command, offset, blockSize);
                offset += blockSize;
            }
            if((output.Length % packetSize) != 0)
            {
                byte[] padding = new byte[packetSize - (output.Length % packetSize)];
                output.Write(padding, 0, padding.Length);
            }
            return output.ToArray();		
        }

        const int VID = 0x2581;
        const int PID = 0x2b7c;
        const int PID_LEDGER = 0x3b7c;
        const int PID_LEDGER_PROTON = 0x4b7c;
        const int HID_BUFFER_SIZE = 64;
        const int SW1_DATA_AVAILABLE = 0x61;
        const int LEDGER_DEFAULT_CHANNEL = 1;
        const int TIMEOUT = 20000;
    }
}
