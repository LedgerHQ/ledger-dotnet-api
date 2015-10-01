using HidLibrary;
using Microsoft.Win32.SafeHandles;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    internal class BTChipTransport
    {
        HidDevice _Device;
        int TAG_APDU = 0x05;
        public BTChipTransport(HidDevice device)
        {
            if(!device.IsOpen)
                device.OpenDevice();
            _Device = device;
        }

        internal byte[] Exchange(byte[] apdu)
        {
            MemoryStream output = new MemoryStream();
            byte[] buffer = new byte[400];
            byte[] paddingBuffer = new byte[MAX_BLOCK];
            int result;
            int length;
            int swOffset;
            uint remaining = (uint)apdu.Length;
            uint offset = 0;

            result = WrapCommandAPDU(DEFAULT_LEDGER_CHANNEL, apdu, LEDGER_HID_PACKET_SIZE, buffer);
            if(result < 0)
            {
                return null;
            }
            remaining = (uint)result;

            while(remaining > 0)
            {
                uint blockSize = (remaining > MAX_BLOCK ? MAX_BLOCK : remaining);
                memset(paddingBuffer, 0, MAX_BLOCK);
                memcpy(paddingBuffer, 0U, buffer, offset, blockSize);

                result = hid_write(_Device.Handle, paddingBuffer, (int)blockSize);
                if(result < 0)
                {
                    return null;
                }
                offset += blockSize;
                remaining -= blockSize;
            }

            buffer = new byte[400];
            result = hid_read_timeout(_Device.Handle, buffer, MAX_BLOCK, TIMEOUT);
            if(result < 0)
            {
                return null;
            }
            offset = MAX_BLOCK;
            for(; ; )
            {
                output = new MemoryStream();
                result = UnwrapReponseAPDU(DEFAULT_LEDGER_CHANNEL, buffer, offset, LEDGER_HID_PACKET_SIZE, output);
                if(result < 0)
                {
                    return null;
                }
                if(result != 0)
                {
                    length = result - 2;
                    swOffset = result - 2;
                    break;
                }
                result = hid_read_timeout(_Device.Handle, buffer, offset, MAX_BLOCK, TIMEOUT);
                if(result < 0)
                {
                    return null;
                }
                offset += MAX_BLOCK;
            }
            return output.ToArray();
        }

        private int hid_read_timeout(IntPtr intPtr, byte[] buffer, uint offset, uint length, int milliseconds)
        {
            var bytes = new byte[length];
            Array.Copy(buffer, offset, bytes, 0, length);
            var result = hid_read_timeout(intPtr, bytes, (uint)length, milliseconds);
            Array.Copy(bytes, 0, buffer, offset, length);
            return result;
        }



        internal int hid_read_timeout(IntPtr hidDeviceObject, byte[] buffer, uint length, int milliseconds)
        {
            var result = this._Device.Read(milliseconds);
            if(result.Status == HidDeviceData.ReadStatus.Success)
            {
                if(result.Data.Length - 1 > length)
                    return -1;
                Array.Copy(result.Data, 1, buffer, 0, length);
                return result.Data.Length;
            }
            return -1;
        }

        internal int hid_write(IntPtr hidDeviceObject, byte[] buffer, int length)
        {
            byte[] sent = new byte[length + 1];
            Array.Copy(buffer, 0, sent, 1, length);
            if(!this._Device.Write(sent))
                return -1;
            Array.Copy(sent, 0, buffer, 0, length);
            return length;
        }


        private void memset(byte[] array, byte value, uint count)
        {
            for(int i = 0; i < count; i++)
                array[i] = value;
        }



        const uint MAX_BLOCK = 64;
        const int VID = 0x2581;
        const int PID = 0x2b7c;
        const int PID_LEDGER = 0x3b7c;
        const int PID_LEDGER_PROTON = 0x4b7c;
        const int HID_BUFFER_SIZE = 64;
        const int SW1_DATA_AVAILABLE = 0x61;
        const int DEFAULT_LEDGER_CHANNEL = 0x0101;
        const int LEDGER_HID_PACKET_SIZE = 64;
        const int TIMEOUT = 20000;


        int WrapCommandAPDU(uint channel, byte[] command, uint packetSize, byte[] output)
        {
            uint commandLength = (uint)command.Length;
            uint outputLength = (uint)output.Length;
            int sequenceIdx = 0;
            uint offset = 0;
            uint offsetOut = 0;
            uint blockSize;
            if(packetSize < 3)
            {
                return -1;
            }
            if(outputLength < 7)
            {
                return -1;
            }
            outputLength -= 7;
            output[offsetOut++] = (byte)((channel >> 8) & 0xff);
            output[offsetOut++] = (byte)(channel & 0xff);
            output[offsetOut++] = (byte)TAG_APDU;
            output[offsetOut++] = (byte)((sequenceIdx >> 8) & 0xff);
            output[offsetOut++] = (byte)(sequenceIdx & 0xff);
            sequenceIdx++;
            output[offsetOut++] = (byte)((commandLength >> 8) & 0xff);
            output[offsetOut++] = (byte)(commandLength & 0xff);
            blockSize = (commandLength > packetSize - 7 ? packetSize - 7 : commandLength);
            if(outputLength < blockSize)
            {
                return -1;
            }
            outputLength -= blockSize;
            memcpy(output, offsetOut, command, offset, blockSize);
            offsetOut += blockSize;
            offset += blockSize;
            while(offset != commandLength)
            {
                if(outputLength < 5)
                {
                    return -1;
                }
                outputLength -= 5;
                output[offsetOut++] = (byte)((channel >> 8) & 0xff);
                output[offsetOut++] = (byte)(channel & 0xff);
                output[offsetOut++] = (byte)TAG_APDU;
                output[offsetOut++] = (byte)((sequenceIdx >> 8) & 0xff);
                output[offsetOut++] = (byte)(sequenceIdx & 0xff);
                sequenceIdx++;
                blockSize = ((commandLength - offset) > packetSize - 5 ? packetSize - 5 : commandLength - offset);
                if(outputLength < blockSize)
                {
                    return -1;
                }
                outputLength -= blockSize;
                memcpy(output, offsetOut, command, offset, blockSize);
                offsetOut += blockSize;
                offset += blockSize;
            }
            while((offsetOut % packetSize) != 0)
            {
                if(outputLength < 1)
                {
                    return -1;
                }
                outputLength--;
                output[offsetOut++] = 0;
            }
            return (int)offsetOut;
        }

        private void memcpy(byte[] dest, uint destOffset, byte[] src, uint srcOffset, uint length)
        {
            Array.Copy(src, srcOffset, dest, destOffset, length);
        }

        private void memcpy(Stream dest, byte[] src, uint srcOffset, uint length)
        {
            dest.Write(src, (int)srcOffset, (int)length);
        }

        int UnwrapReponseAPDU(uint channel, byte[] data, uint dataLength, uint packetSize, Stream output)
        {
            int sequenceIdx = 0;
            uint offset = 0;
            uint offsetOut = 0;
            uint responseLength;
            uint blockSize;
            if((data == null) || (dataLength < 7 + 5))
            {
                return 0;
            }
            if(data[offset++] != ((channel >> 8) & 0xff))
            {
                return -1;
            }
            if(data[offset++] != (channel & 0xff))
            {
                return -1;
            }
            if(data[offset++] != TAG_APDU)
            {
                return -1;
            }
            if(data[offset++] != ((sequenceIdx >> 8) & 0xff))
            {
                return -1;
            }
            if(data[offset++] != (sequenceIdx & 0xff))
            {
                return -1;
            }
            responseLength = (((uint)data[offset++]) << 8);
            responseLength |= data[offset++];

            if(dataLength < 7 + responseLength)
            {
                return 0;
            }
            blockSize = (responseLength > packetSize - 7 ? packetSize - 7 : responseLength);
            memcpy(output, data, offset, blockSize);
            offset += blockSize;
            offsetOut += blockSize;
            while(offsetOut != responseLength)
            {
                sequenceIdx++;
                if(offset == dataLength)
                {
                    return 0;
                }
                if(data[offset++] != ((channel >> 8) & 0xff))
                {
                    return -1;
                }
                if(data[offset++] != (channel & 0xff))
                {
                    return -1;
                }
                if(data[offset++] != TAG_APDU)
                {
                    return -1;
                }
                if(data[offset++] != ((sequenceIdx >> 8) & 0xff))
                {
                    return -1;
                }
                if(data[offset++] != (sequenceIdx & 0xff))
                {
                    return -1;
                }
                blockSize = ((responseLength - offsetOut) > packetSize - 5 ? packetSize - 5 : responseLength - offsetOut);
                if(blockSize > dataLength - offset)
                {
                    return 0;
                }
                memcpy(output, data, offset, blockSize);
                offset += blockSize;
                offsetOut += blockSize;
            }
            return (int)offsetOut;
        }

    }

}
