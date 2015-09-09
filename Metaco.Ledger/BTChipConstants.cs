using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaco.Ledger
{
    public class BTChipConstants
    {

        public const byte BTCHIP_CLA = (byte)0xE0;
        public const byte BTCHIP_ADM_CLA = (byte)0xD0;

        public const byte BTCHIP_INS_SETUP = (byte)0x20;
        public const byte BTCHIP_INS_VERIFY_PIN = (byte)0x22;
        public const byte BTCHIP_INS_GET_OPERATION_MODE = (byte)0x24;
        public const byte BTCHIP_INS_SET_OPERATION_MODE = (byte)0x26;
        public const byte BTCHIP_INS_SET_KEYMAP = (byte)0x28;
        public const byte BTCHIP_INS_SET_COMM_PROTOCOL = (byte)0x2a;
        public const byte BTCHIP_INS_GET_WALLET_PUBLIC_KEY = (byte)0x40;
        public const byte BTCHIP_INS_GET_TRUSTED_INPUT = (byte)0x42;
        public const byte BTCHIP_INS_HASH_INPUT_START = (byte)0x44;
        public const byte BTCHIP_INS_HASH_INPUT_FINALIZE = (byte)0x46;
        public const byte BTCHIP_INS_HASH_SIGN = (byte)0x48;
        public const byte BTCHIP_INS_HASH_INPUT_FINALIZE_FULL = (byte)0x4a;
        public const byte BTCHIP_INS_GET_INTERNAL_CHAIN_INDEX = (byte)0x4c;
        public const byte BTCHIP_INS_SIGN_MESSAGE = (byte)0x4e;
        public const byte BTCHIP_INS_GET_TRANSACTION_LIMIT = (byte)0xa0;
        public const byte BTCHIP_INS_SET_TRANSACTION_LIMIT = (byte)0xa2;
        public const byte BTCHIP_INS_IMPORT_PRIVATE_KEY = (byte)0xb0;
        public const byte BTCHIP_INS_GET_PUBLIC_KEY = (byte)0xb2;
        public const byte BTCHIP_INS_DERIVE_BIP32_KEY = (byte)0xb4;
        public const byte BTCHIP_INS_SIGNVERIFY_IMMEDIATE = (byte)0xb6;
        public const byte BTCHIP_INS_GET_RANDOM = (byte)0xc0;
        public const byte BTCHIP_INS_GET_ATTESTATION = (byte)0xc2;
        public const byte BTCHIP_INS_GET_FIRMWARE_VERSION = (byte)0xc4;
        public const byte BTCHIP_INS_COMPOSE_MOFN_ADDRESS = (byte)0xc6;
        public const byte BTCHIP_INS_GET_POS_SEED = (byte)0xca;

        public const byte BTCHIP_INS_ADM_SET_KEYCARD_SEED = (byte)0x26;

        public static byte[] QWERTY_KEYMAP = HexEncoder.Instance.DecodeData("000000000000000000000000760f00d4ffffffc7000000782c1e3420212224342627252e362d3738271e1f202122232425263333362e37381f0405060708090a0b0c0d0e0f101112131415161718191a1b1c1d2f3130232d350405060708090a0b0c0d0e0f101112131415161718191a1b1c1d2f313035");
        public static byte[] AZERTY_KEYMAP = HexEncoder.Instance.DecodeData("08000000010000200100007820c8ffc3feffff07000000002c38202030341e21222d352e102e3637271e1f202122232425263736362e37101f1405060708090a0b0c0d0e0f331112130415161718191d1b1c1a2f64302f2d351405060708090a0b0c0d0e0f331112130415161718191d1b1c1a2f643035");

        public const int SW_OK = 0x9000;
        public const int SW_INS_NOT_SUPPORTED = 0x6D00;
        public const int SW_WRONG_P1_P2 = 0x6B00;
        public const int SW_INCORRECT_P1_P2 = 0x6A86;
    }
}
