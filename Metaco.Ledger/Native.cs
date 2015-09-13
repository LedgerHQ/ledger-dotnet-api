using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    internal class AnonymousHandle : SafeHandle
    {
        int _Context;
        Action<AnonymousHandle> _Release;
        public AnonymousHandle(int context, Action<AnonymousHandle> release)
            : base(new IntPtr(context), true)
        {
            _Release = release;
            _Context = context;
        }

        public int Handle
        {
            get
            {
                return _Context;
            }
        }


        public override bool IsInvalid
        {
            get
            {
                return false;
            }
        }

        protected override bool ReleaseHandle()
        {
            _Release(this);
            return true;
        }
    }
    internal class ModWinsCard
    {
        public const int SCARD_S_SUCCESS = 0;
        public const int SCARD_ATR_LENGTH = 33;
        public const string winscardDLLFName = "winscard.dll";



        [StructLayout(LayoutKind.Sequential)]
        public struct APDURec
        {
            public bool ForSend;

            /// <summary>
            /// The T=0 instruction class.
            /// </summary>
            public byte bCLA;

            /// <summary>
            /// An instruction code in the T=0 instruction class.
            /// </summary>
            public byte bINS;

            /// <summary>
            /// Reference codes that complete the instruction code.
            /// </summary>
            public byte bP1;

            /// <summary>
            /// Reference codes that complete the instruction code.
            /// </summary>
            public byte bP2;

            /// <summary>
            /// The number of data bytes to be transmitted during the command, per ISO 7816-4, Section 8.2.1.
            /// </summary>
            public byte bP3;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] Data;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] SW;
        }

        /// <summary>
        /// The SCARD_IO_REQUEST structure begins a protocol control information structure. Any protocol-specific information then immediately follows this structure. The entire length of the structure must be aligned with the underlying hardware architecture word size. For example, in Win32 the length of any PCI information must be a multiple of four bytes so that it aligns on a 32-bit boundary.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SCARD_IO_REQUEST
        {
            /// <summary>
            /// Protocol in use. 
            /// </summary>
            public int dwProtocol;

            /// <summary>
            /// Length, in bytes, of the SCARD_IO_REQUEST structure plus any following PCI-specific information.
            /// </summary>
            public int cbPciLength;
        }

        /// <summary>
        /// The SCARD_READERSTATE structure is used by functions for tracking smart cards within readers.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SCARD_READERSTATE
        {
            /// <summary>
            /// Pointer to the name of the reader being monitored.
            /// </summary>
            string RdrName;

            /// <summary>
            /// Not used by the smart card subsystem. This member is used by the application.
            /// </summary>
            long UserData;

            /// <summary>
            /// Current state of the reader, as seen by the application. This field can take on any of the following values, in combination, as a bit mask. 
            /// </summary>
            long RdrCurrState;

            /// <summary>
            /// Current state of the reader, as known by the smart card resource manager. This field can take on any of the following values, in combination, as a bit mask. 
            /// </summary>
            long RdrEventState;

            /// <summary>
            /// Number of bytes in the returned ATR. 
            /// </summary>
            long ATRLength;

            /// <summary>
            /// ATR of the inserted card, with extra alignment bytes. 
            /// </summary>
            byte[] ATRValue;
        }

        #region Memory Card Type

        public const int CT_MCU = 0x00;                   // MCU
        public const int CT_IIC_Auto = 0x01;               // IIC (Auto Detect Memory Size)
        public const int CT_IIC_1K = 0x02;                 // IIC (1K)
        public const int CT_IIC_2K = 0x03;                 // IIC (2K)
        public const int CT_IIC_4K = 0x04;                 // IIC (4K)
        public const int CT_IIC_8K = 0x05;                 // IIC (8K)
        public const int CT_IIC_16K = 0x06;                // IIC (16K)
        public const int CT_IIC_32K = 0x07;                // IIC (32K)
        public const int CT_IIC_64K = 0x08;                // IIC (64K)
        public const int CT_IIC_128K = 0x09;               // IIC (128K)
        public const int CT_IIC_256K = 0x0A;               // IIC (256K)
        public const int CT_IIC_512K = 0x0B;               // IIC (512K)
        public const int CT_IIC_1024K = 0x0C;              // IIC (1024K)
        public const int CT_AT88SC153 = 0x0D;              // AT88SC153
        public const int CT_AT88SC1608 = 0x0E;             // AT88SC1608
        public const int CT_SLE4418 = 0x0F;                // SLE4418
        public const int CT_SLE4428 = 0x10;                // SLE4428
        public const int CT_SLE4432 = 0x11;                // SLE4432
        public const int CT_SLE4442 = 0x12;                // SLE4442
        public const int CT_SLE4406 = 0x13;                // SLE4406
        public const int CT_SLE4436 = 0x14;                // SLE4436
        public const int CT_SLE5536 = 0x15;                // SLE5536
        public const int CT_MCUT0 = 0x16;                  // MCU T=0
        public const int CT_MCUT1 = 0x17;                  // MCU T=1
        public const int CT_MCU_Auto = 0x18;               // MCU Autodetect

        #endregion

        #region Context Scope

        /// <summary>
        /// The context is a user context, and any database operations 
        /// are performed within the domain of the user.
        /// </summary>
        public const int SCARD_SCOPE_USER = 0;

        /// <summary>
        /// The context is that of the current terminal, and any database 
        /// operations are performed within the domain of that terminal.  
        /// (The calling application must have appropriate access permissions 
        /// for any database actions.)
        /// </summary>
        public const int SCARD_SCOPE_TERMINAL = 1;

        /// <summary>
        /// The context is the system context, and any database operations 
        /// are performed within the domain of the system.  (The calling
        /// application must have appropriate access permissions for any 
        /// database actions.)
        /// </summary>
        public const int SCARD_SCOPE_SYSTEM = 2;

        /// <summary>
        /// The application is unaware of the current state, and would like 
        /// to know. The use of this value results in an immediate return
        /// from state transition monitoring services. This is represented
        /// by all bits set to zero.
        /// </summary>
        public const int SCARD_STATE_UNAWARE = 0x00;

        /// <summary>
        /// The application requested that this reader be ignored. No other
        /// bits will be set.
        /// </summary>
        public const int SCARD_STATE_IGNORE = 0x01;

        /// <summary>
        /// This implies that there is a difference between the state 
        /// believed by the application, and the state known by the Service
        /// Manager.When this bit is set, the application may assume a
        /// significant state change has occurred on this reader.
        /// </summary>
        public const int SCARD_STATE_CHANGED = 0x02;

        /// <summary>
        /// This implies that the given reader name is not recognized by
        /// the Service Manager. If this bit is set, then SCARD_STATE_CHANGED
        /// and SCARD_STATE_IGNORE will also be set.
        /// </summary>
        public const int SCARD_STATE_UNKNOWN = 0x04;

        /// <summary>
        /// This implies that the actual state of this reader is not
        /// available. If this bit is set, then all the following bits are
        /// clear.
        /// </summary>
        public const int SCARD_STATE_UNAVAILABLE = 0x08;

        /// <summary>
        /// This implies that there is not card in the reader.  If this bit
        /// is set, all the following bits will be clear.
        /// </summary>
        public const int SCARD_STATE_EMPTY = 0x10;

        /// <summary>
        /// This implies that there is a card in the reader. 
        /// </summary>
        public const int SCARD_STATE_PRESENT = 0x20;

        /// <summary>
        /// This implies that there is a card in the reader with an ATR
        /// matching one of the target cards. If this bit is set,
        /// SCARD_STATE_PRESENT will also be set.  This bit is only returned
        /// on the SCardLocateCard() service.
        /// </summary>
        public const int SCARD_STATE_ATRMATCH = 0x40;

        /// <summary>
        /// This implies that the card in the reader is allocated for 
        /// exclusive use by another application. If this bit is set,
        /// SCARD_STATE_PRESENT will also be set.
        /// </summary>
        public const int SCARD_STATE_EXCLUSIVE = 0x80;

        /// <summary>
        /// This implies that the card in the reader is in use by one or 
        /// more other applications, but may be connected to in shared mode. 
        /// If this bit is set, SCARD_STATE_PRESENT will also be set.
        /// </summary>
        public const int SCARD_STATE_INUSE = 0x100;

        /// <summary>
        /// This implies that the card in the reader is unresponsive or not
        /// supported by the reader or software.
        /// </summary>
        public const int SCARD_STATE_MUTE = 0x200;

        /// <summary>
        /// This implies that the card in the reader has not been powered up. 
        /// </summary>
        public const int SCARD_STATE_UNPOWERED = 0x400;

        /// <summary>
        /// This application is not willing to share this card with other 
        /// applications.
        /// </summary>
        public const int SCARD_SHARE_EXCLUSIVE = 1;

        /// <summary>
        /// This application is willing to share this card with other 
        /// applications.
        /// </summary>
        public const int SCARD_SHARE_SHARED = 2;

        /// <summary>
        /// This application demands direct control of the reader, so it 
        /// is not available to other applications.
        /// </summary>
        public const int SCARD_SHARE_DIRECT = 3;

        #endregion

        #region Disposition

        /// <summary>
        /// Don't do anything special on close
        /// </summary>
        public const int SCARD_LEAVE_CARD = 0;

        /// <summary>
        /// Reset the card on close
        /// </summary>
        public const int SCARD_RESET_CARD = 1;

        /// <summary>
        /// Power down the card on close
        /// </summary>
        public const int SCARD_UNPOWER_CARD = 2;

        /// <summary>
        /// Eject the card on close
        /// </summary>
        public const int SCARD_EJECT_CARD = 3;

        #endregion

        #region ACS IOCTL Class

        public const long FILE_DEVICE_SMARTCARD = 0x310000; // Reader action IOCTLs
        public const long IOCTL_SMARTCARD_DIRECT = FILE_DEVICE_SMARTCARD + 2050 * 4;
        public const long IOCTL_SMARTCARD_SELECT_SLOT = FILE_DEVICE_SMARTCARD + 2051 * 4;
        public const long IOCTL_SMARTCARD_DRAW_LCDBMP = FILE_DEVICE_SMARTCARD + 2052 * 4;
        public const long IOCTL_SMARTCARD_DISPLAY_LCD = FILE_DEVICE_SMARTCARD + 2053 * 4;
        public const long IOCTL_SMARTCARD_CLR_LCD = FILE_DEVICE_SMARTCARD + 2054 * 4;
        public const long IOCTL_SMARTCARD_READ_KEYPAD = FILE_DEVICE_SMARTCARD + 2055 * 4;
        public const long IOCTL_SMARTCARD_READ_RTC = FILE_DEVICE_SMARTCARD + 2057 * 4;
        public const long IOCTL_SMARTCARD_SET_RTC = FILE_DEVICE_SMARTCARD + 2058 * 4;
        public const long IOCTL_SMARTCARD_SET_OPTION = FILE_DEVICE_SMARTCARD + 2059 * 4;
        public const long IOCTL_SMARTCARD_SET_LED = FILE_DEVICE_SMARTCARD + 2060 * 4;
        public const long IOCTL_SMARTCARD_LOAD_KEY = FILE_DEVICE_SMARTCARD + 2062 * 4;
        public const long IOCTL_SMARTCARD_READ_EEPROM = FILE_DEVICE_SMARTCARD + 2065 * 4;
        public const long IOCTL_SMARTCARD_WRITE_EEPROM = FILE_DEVICE_SMARTCARD + 2066 * 4;
        public const long IOCTL_SMARTCARD_GET_VERSION = FILE_DEVICE_SMARTCARD + 2067 * 4;
        public const long IOCTL_SMARTCARD_GET_READER_INFO = FILE_DEVICE_SMARTCARD + 2051 * 4;
        public const long IOCTL_SMARTCARD_SET_CARD_TYPE = FILE_DEVICE_SMARTCARD + 2060 * 4;

        #endregion

        #region Error Codes
        public const int SCARD_F_INTERNAL_ERROR = -2146435071;
        public const int SCARD_E_CANCELLED = -2146435070;
        public const int SCARD_E_INVALID_HANDLE = -2146435069;
        public const int SCARD_E_INVALID_PARAMETER = -2146435068;
        public const int SCARD_E_INVALID_TARGET = -2146435067;
        public const int SCARD_E_NO_MEMORY = -2146435066;
        public const int SCARD_F_WAITED_TOO_LONG = -2146435065;
        public const int SCARD_E_INSUFFICIENT_BUFFER = -2146435064;
        public const int SCARD_E_UNKNOWN_READER = -2146435063;
        public const int SCARD_E_NO_READERS_AVAILABLE = -2146435026;


        public const int SCARD_E_TIMEOUT = -2146435062;
        public const int SCARD_E_SHARING_VIOLATION = -2146435061;
        public const int SCARD_E_NO_SMARTCARD = -2146435060;
        public const int SCARD_E_UNKNOWN_CARD = -2146435059;
        public const int SCARD_E_CANT_DISPOSE = -2146435058;
        public const int SCARD_E_PROTO_MISMATCH = -2146435057;


        public const int SCARD_E_NOT_READY = -2146435056;
        public const int SCARD_E_INVALID_VALUE = -2146435055;
        public const int SCARD_E_SYSTEM_CANCELLED = -2146435054;
        public const int SCARD_F_COMM_ERROR = -2146435053;
        public const int SCARD_F_UNKNOWN_ERROR = -2146435052;
        public const int SCARD_E_INVALID_ATR = -2146435051;
        public const int SCARD_E_NOT_TRANSACTED = -2146435050;
        public const int SCARD_E_READER_UNAVAILABLE = -2146435049;
        public const int SCARD_P_SHUTDOWN = -2146435048;
        public const int SCARD_E_PCI_TOO_SMALL = -2146435047;

        public const int SCARD_E_READER_UNSUPPORTED = -2146435046;
        public const int SCARD_E_DUPLICATE_READER = -2146435045;
        public const int SCARD_E_CARD_UNSUPPORTED = -2146435044;
        public const int SCARD_E_NO_SERVICE = -2146435043;
        public const int SCARD_E_SERVICE_STOPPED = -2146435042;

        public const int SCARD_W_UNSUPPORTED_CARD = -2146435041;
        public const int SCARD_W_UNRESPONSIVE_CARD = -2146435040;
        public const int SCARD_W_UNPOWERED_CARD = -2146435039;
        public const int SCARD_W_RESET_CARD = -2146435038;

        //From SCARD_W_REMOVED_CARD to SCARD_E_DIR_NOT_FOUND
        public const int SCARD_E_DIR_NOT_FOUND = -2146435037;

        public const int SCARD_W_REMOVED_CARD = -2146434967;
        #endregion

        #region Protocol
        /// <summary>
        /// There is no active protocol.
        /// </summary>
        public const int SCARD_PROTOCOL_UNDEFINED = 0x00;

        /// <summary>
        /// T=0 is the active protocol.
        /// </summary>
        public const int SCARD_PROTOCOL_T0 = 0x01;

        /// <summary>
        /// T=1 is the active protocol.
        /// </summary>
        public const int SCARD_PROTOCOL_T1 = 0x02;

        /// <summary>
        /// Raw is the active protocol.
        /// </summary>
        public const int SCARD_PROTOCOL_RAW = 0x10000;
        //public const int SCARD_PROTOCOL_DEFAULT = 0x80000000;      // Use implicit PTS.

        #endregion

        #region Reader State

        /// <summary>
        /// This value implies the driver is unaware of the current 
        /// state of the reader.
        /// </summary>
        public const int SCARD_UNKNOWN = 0;

        /// <summary>
        /// This value implies there is no card in the reader.
        /// </summary>
        public const int SCARD_ABSENT = 1;

        /// <summary>
        /// This value implies there is a card is present in the reader, 
        /// but that it has not been moved into position for use.        
        /// </summary>
        public const int SCARD_PRESENT = 2;

        /// <summary>
        /// This value implies there is a card in the reader in position 
        /// for use.  The card is not powered.
        /// </summary>
        public const int SCARD_SWALLOWED = 3;

        /// <summary>
        /// This value implies there is power is being provided to the card, 
        /// but the Reader Driver is unaware of the mode of the card.
        /// </summary>
        public const int SCARD_POWERED = 4;

        /// <summary>
        /// This value implies the card has been reset and is awaiting 
        /// PTS negotiation.
        /// </summary>
        public const int SCARD_NEGOTIABLE = 5;

        /// <summary>
        /// This value implies the card has been reset and specific 
        /// communication protocols have been established.
        /// </summary>
        public const int SCARD_SPECIFIC = 6;

        #endregion

        #region Prototypes

        /// <summary>
        /// The SCardEstablishContext function establishes the resource manager context (the scope) within which database operations are performed.
        /// </summary>
        /// <param name="dwScope">[in] Scope of the resource manager context. This parameter can be one of the following values.</param>
        /// <param name="pvReserved1">[in] Reserved for future use and must be NULL. This parameter will allow a suitably privileged management application to act on behalf of another user.</param>
        /// <param name="pvReserved2">[in] Reserved for future use and must be NULL. </param>
        /// <param name="phContext">[out] Handle to the established resource manager context. This handle can now be supplied to other functions attempting to do work within this context.</param>
        /// <returns></returns>       
        [DllImport(winscardDLLFName)]
        public static extern int SCardEstablishContext(int dwScope, int pvReserved1, int pvReserved2, ref int phContext);

        /// <summary>
        /// The SCardReleaseContext function closes an established resource manager context, freeing any resources allocated under that context, including SCARDHANDLE objects and memory allocated using the SCARD_AUTOALLOCATE length designator.
        /// </summary>
        /// <param name="phContext">[in] Handle that identifies the resource manager context. The resource manager context is set by a previous call to SCardEstablishContext.</param>
        /// <returns></returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardReleaseContext(int phContext);


        /// <summary>
        /// The SCardConnect function establishes a connection (using a specific resource manager context) between the calling application and a smart card contained by a specific reader. If no card exists in the specified reader, an error is returned.
        /// </summary>
        /// <param name="hContext">[in] A handle that identifies the resource manager context. The resource manager context is set by a previous call to SCardEstablishContext.</param>
        /// <param name="szReaderName">[in] The name of the reader that contains the target card. </param>
        /// <param name="dwShareMode">[in] A flag that indicates whether other applications may form connections to the card.</param>
        /// <param name="dwPrefProtocol">[in] A bit mask of acceptable protocols for the connection. Possible values may be combined with the OR operation.</param>
        /// <param name="phCard">[out] A handle that identifies the connection to the smart card in the designated reader. </param>
        /// <param name="ActiveProtocol">[out] A flag that indicates the established active protocol.</param>
        /// <returns></returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardConnect(int hContext, string szReaderName, int dwShareMode, int dwPrefProtocol, ref int phCard, ref int ActiveProtocol);

        /// <summary>
        /// The SCardBeginTransaction function starts a transaction, waiting for the completion of all other transactions before it begins.
        /// When the transaction starts, all other applications are blocked from accessing the smart card while the transaction is in progress.
        /// </summary>
        /// <param name="hCard">[in] Reference value obtained from a previous call to SCardConnect.</param>
        /// <returns>This function returns different values depending on whether it succeeds or fails.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardBeginTransaction(int hCard);

        /// <summary>
        /// The SCardDisconnect function terminates a connection previously opened between the calling application and a smart card in the target reader.
        /// </summary>
        /// <param name="hCard">[in] Reference value obtained from a previous call to SCardConnect. </param>
        /// <param name="Disposition">[in] Action to take on the card in the connected reader on close. </param>
        /// <returns>This function returns different values depending on whether it succeeds or fails.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardDisconnect(int hCard, int Disposition);

        /// <summary>
        /// The SCardListReaderGroups function provides the list of reader groups that have previously been introduced to the system.
        /// </summary>
        /// <param name="hContext">[in] Handle that identifies the resource manager context for the query. The resource manager context can be set by a previous call to SCardEstablishContext. This parameter cannot be NULL.</param>
        /// <param name="mzGroups">[out] Multi-string that lists the reader groups defined to the system and available to the current user on the current terminal. If this value is NULL, SCardListReaderGroups ignores the buffer length supplied in pcchGroups, writes the length of the buffer that would have been returned if this parameter had not been NULL to pcchGroups, and returns a success code.</param>
        /// <param name="pcchGroups">[in, out] Length of the mszGroups buffer in characters, and receives the actual length of the multi-string structure, including all trailing null characters. If the buffer length is specified as SCARD_AUTOALLOCATE, then mszGroups is converted to a pointer to a byte pointer, and receives the address of a block of memory containing the multi-string structure. This block of memory must be deallocated with SCardFreeMemory. </param>
        /// <returns>This function returns different values depending on whether it succeeds or fails.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardListReaderGroups(int hContext, ref string mzGroups, ref int pcchGroups);

        /// <summary>
        /// The SCardListReaders function provides the list of readers within a set of named reader groups, eliminating duplicates.
        /// The caller supplies a list of reader groups, and receives the list of readers within the named groups. Unrecognized group names are ignored.
        /// </summary>
        /// <param name="hContext">[in] Handle that identifies the resource manager context for the query. The resource manager context can be set by a previous call to SCardEstablishContext. This parameter cannot be NULL.</param>
        /// <param name="Groups">[in] Names of the reader groups defined to the system, as a multi-string. Use a NULL value to list all readers in the system (that is, the SCard$AllReaders group). </param>
        /// <param name="Readers">[out] Multi-string that lists the card readers within the supplied reader groups. If this value is NULL, SCardListReaders ignores the buffer length supplied in pcchReaders, writes the length of the buffer that would have been returned if this parameter had not been NULL to pcchReaders, and returns a success code.</param>
        /// <param name="pcchReaders">[in, out] Length of the mszReaders buffer in characters. This parameter receives the actual length of the multi-string structure, including all trailing null characters. If the buffer length is specified as SCARD_AUTOALLOCATE, then mszReaders is converted to a pointer to a byte pointer, and receives the address of a block of memory containing the multi-string structure. This block of memory must be deallocated with SCardFreeMemory.</param>
        /// <returns>This function returns different values depending on whether it succeeds or fails.</returns>
        [DllImport(winscardDLLFName, EntryPoint = "SCardListReadersA", CharSet = CharSet.Ansi)]
        public static extern int SCardListReaders(
            int hContext,
            byte[] Groups,
            byte[] Readers,
            ref int pcchReaders
            );

        /// <summary>
        /// The SCardStatus function provides the current status of a smart card in a reader. You can call it any time after a successful call to SCardConnect and before a successful call to SCardDisconnect. It does not affect the state of the reader or reader driver.
        /// </summary>
        /// <param name="hCard">[in] Reference value returned from SCardConnect. </param>
        /// <param name="szReaderName">[out] List of friendly names (multiple string) by which the currently connected reader is known. </param>
        /// <param name="pcchReaderLen">[in, out] On input, supplies the length of the szReaderName buffer. 
        /// On output, receives the actual length (in characters) of the reader name list, including the trailing NULL character. If this buffer length is specified as SCARD_AUTOALLOCATE, then szReaderName is converted to a pointer to a byte pointer, and it receives the address of a block of memory that contains the multiple-string structure.</param>
        /// <param name="State">[out] Current state of the smart card in the reader. Upon success, it receives one of the following state indicators.</param>
        /// <param name="Protocol">[out] Current protocol, if any. The returned value is meaningful only if the returned value of pdwState is SCARD_SPECIFICMODE.</param>
        /// <param name="ATR">[out] Pointer to a 32-byte buffer that receives the ATR string from the currently inserted card, if available.</param>
        /// <param name="ATRLen">[in, out] On input, supplies the length of the pbAtr buffer. On output, receives the number of bytes in the ATR string (32 bytes maximum). If this buffer length is specified as SCARD_AUTOALLOCATE, then pbAtr is converted to a pointer to a byte pointer, and it receives the address of a block of memory that contains the multiple-string structure.</param>
        /// <returns>If the function successfully provides the current status of a smart card in a reader, the return value is SCARD_S_SUCCESS.
        /// If the function fails, it returns an error code. For more information, see Smart Card Return Values.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardStatus(int hCard, string szReaderName, ref int pcchReaderLen, ref int State, ref int Protocol, ref byte ATR, ref int ATRLen);


        /// <summary>
        /// The SCardEndTransaction function completes a previously declared transaction, allowing other applications to resume interactions with the card.
        /// </summary>
        /// <param name="hCard">[in] Reference value obtained from a previous call to SCardConnect. This value would also have been used in an earlier call to SCardBeginTransaction.</param>
        /// <param name="Disposition">[in] Action to take on the card in the connected reader on close. </param>
        /// <returns>This function returns different values depending on whether it succeeds or fails.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardEndTransaction(int hCard, int Disposition);

        [DllImport(winscardDLLFName)]
        public static extern int SCardState(int hCard, ref uint State, ref uint Protocol, ref byte ATR, ref uint ATRLen);

        /// <summary>
        /// The SCardTransmit function sends a service request to the smart card and expects to receive data back from the card.
        /// </summary>
        /// <param name="hCard">[in] A reference value returned from the SCardConnect function.</param>
        /// <param name="pioSendRequest">[in] A pointer to the protocol header structure for the instruction. This buffer is in the format of an SCARD_IO_REQUEST structure, followed by the specific protocol control information (PCI).
        /// For the T=0, T=1, and Raw protocols, the PCI structure is constant. The smart card subsystem supplies a global T=0, T=1, or Raw PCI structure, which you can reference by using the symbols SCARD_PCI_T0, SCARD_PCI_T1, and SCARD_PCI_RAW respectively.</param>
        /// <param name="SendBuff">[in] A pointer to the actual data to be written to the card. </param>
        /// <param name="SendBuffLen">[in] The length, in bytes, of the pbSendBuffer parameter. </param>
        /// <param name="pioRecvRequest">[in, out] Pointer to the protocol header structure for the instruction, followed by a buffer in which to receive any returned protocol control information (PCI) specific to the protocol in use. This parameter can be NULL if no PCI is returned. </param>
        /// <param name="RecvBuff">[out] Pointer to any data returned from the card. </param>
        /// <param name="RecvBuffLen">[in, out] Supplies the length, in bytes, of the pbRecvBuffer parameter and receives the actual number of bytes received from the smart card. This value cannot be SCARD_AUTOALLOCATE because SCardTransmit does not support SCARD_AUTOALLOCATE.</param>
        /// <returns>If the function successfully sends a service request to the smart card, the return value is SCARD_S_SUCCESS.
        /// If the function fails, it returns an error code. For more information, see Smart Card Return Values.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardTransmit(int hCard, ref SCARD_IO_REQUEST pioSendRequest, ref byte SendBuff, int SendBuffLen, ref SCARD_IO_REQUEST pioRecvRequest, ref byte RecvBuff, ref int RecvBuffLen);

        /// <summary>
        /// The SCardControl function gives you direct control of the reader. You can call it any time after a successful call to SCardConnect and before a successful call to SCardDisconnect. The effect on the state of the reader depends on the control code.
        /// </summary>
        /// <param name="hCard">[in] Reference value returned from SCardConnect. </param>
        /// <param name="dwControlCode">[in] Control code for the operation. This value identifies the specific operation to be performed.</param>
        /// <param name="SendBuff">[in] Pointer to a buffer that contains the data required to perform the operation. This parameter can be NULL if the dwControlCode parameter specifies an operation that does not require input data. </param>
        /// <param name="SendBuffLen">[in] Size, in bytes, of the buffer pointed to by lpInBuffer. </param>
        /// <param name="RecvBuff">[out] Pointer to a buffer that receives the operation's output data. This parameter can be NULL if the dwControlCode parameter specifies an operation that does not produce output data. </param>
        /// <param name="RecvBuffLen">[in] Size, in bytes, of the buffer pointed to by lpOutBuffer. </param>
        /// <param name="pcbBytesReturned">[out] Pointer to a DWORD that receives the size, in bytes, of the data stored into the buffer pointed to by lpOutBuffer. </param>
        /// <returns>This function returns different values depending on whether it succeeds or fails.</returns>
        [DllImport(winscardDLLFName)]
        public static extern int SCardControl(int hCard, uint dwControlCode, ref byte SendBuff, int SendBuffLen, ref byte RecvBuff, int RecvBuffLen, ref int pcbBytesReturned);

        #endregion

        #region Miscellaneous

        /// <summary>
        /// Returns the specific error message
        /// </summary>
        /// <param name="errCode">The error code</param>
        /// <returns></returns>
        public static string GetScardErrMsg(long errCode)
        {
            switch(errCode)
            {
                case SCARD_E_CANCELLED:
                    return ("The action was canceled by an SCardCancel request.");
                case SCARD_E_CANT_DISPOSE:
                    return ("The system could not dispose of the media in the requested manner.");
                case SCARD_E_CARD_UNSUPPORTED:
                    return ("The smart card does not meet minimal requirements for support.");
                case SCARD_E_DUPLICATE_READER:
                    return ("The reader driver didn't produce a unique reader name.");
                case SCARD_E_INSUFFICIENT_BUFFER:
                    return ("The data buffer for returned data is too small for the returned data.");
                case SCARD_E_INVALID_ATR:
                    return ("An ATR string obtained from the registry is not a valid ATR string.");
                case SCARD_E_INVALID_HANDLE:
                    return ("The supplied handle was invalid.");
                case SCARD_E_INVALID_PARAMETER:
                    return ("One or more of the supplied parameters could not be properly interpreted.");
                case SCARD_E_INVALID_TARGET:
                    return ("Registry startup information is missing or invalid.");
                case SCARD_E_INVALID_VALUE:
                    return ("One or more of the supplied parameter values could not be properly interpreted.");
                case SCARD_E_NOT_READY:
                    return ("The reader or card is not ready to accept commands.");
                case SCARD_E_NOT_TRANSACTED:
                    return ("An attempt was made to end a non-existent transaction.");
                case SCARD_E_NO_MEMORY:
                    return ("Not enough memory available to complete this command.");
                case SCARD_E_NO_SERVICE:
                    return ("The smart card resource manager is not running.");
                case SCARD_E_NO_SMARTCARD:
                    return ("The operation requires a smart card, but no smart card is currently in the device.");
                case SCARD_E_PCI_TOO_SMALL:
                    return ("The PCI receive buffer was too small.");
                case SCARD_E_PROTO_MISMATCH:
                    return ("The requested protocols are incompatible with the protocol currently in use with the card.");
                case SCARD_E_READER_UNAVAILABLE:
                    return ("The specified reader is not currently available for use.");
                case SCARD_E_READER_UNSUPPORTED:
                    return ("The reader driver does not meet minimal requirements for support.");
                case SCARD_E_SERVICE_STOPPED:
                    return ("The smart card resource manager has shut down.");
                case SCARD_E_SHARING_VIOLATION:
                    return ("The smart card cannot be accessed because of other outstanding connections.");
                case SCARD_E_SYSTEM_CANCELLED:
                    return ("The action was canceled by the system, presumably to log off or shut down.");
                case SCARD_E_TIMEOUT:
                    return ("The user-specified timeout value has expired.");
                case SCARD_E_UNKNOWN_CARD:
                    return ("The specified smart card name is not recognized.");
                case SCARD_E_UNKNOWN_READER:
                    return ("The specified reader name is not recognized.");
                case SCARD_E_NO_READERS_AVAILABLE:
                    return ("No smart card reader is available.");
                case SCARD_F_COMM_ERROR:
                    return ("An internal communications error has been detected.");
                case SCARD_F_INTERNAL_ERROR:
                    return ("An internal consistency check failed.");
                case SCARD_F_UNKNOWN_ERROR:
                    return ("An internal error has been detected, but the source is unknown.");
                case SCARD_F_WAITED_TOO_LONG:
                    return ("An internal consistency timer has expired.");
                case SCARD_S_SUCCESS:
                    return ("No error was encountered.");
                case SCARD_E_DIR_NOT_FOUND:
                    return ("The identified directory does not exist in the smart card..");
                case SCARD_W_RESET_CARD:
                    return ("The smart card has been reset, so any shared state information is invalid.");
                case SCARD_W_UNPOWERED_CARD:
                    return ("Power has been removed from the smart card, so that further communication is not possible.");
                case SCARD_W_UNRESPONSIVE_CARD:
                    return ("The smart card is not responding to a reset.");
                case SCARD_W_UNSUPPORTED_CARD:
                    return ("The reader cannot communicate with the card, due to ATR string configuration conflicts.");
                case SCARD_W_REMOVED_CARD:
                    return ("The smart card has been removed, so further communication is not possible.");
                default:
                    return ("Code: " + errCode + "\r\nDescription: " + "Undocumented error.");
            }
        }

        /// <summary>
        /// Returns the type of card
        /// </summary>
        /// <param name="sel_Res"></param>
        /// <returns></returns>
        public static string GetTagType(byte sel_Res)
        {
            switch(sel_Res)
            {
                case 0x00:
                    return ("MIFARE Ultralight");
                case 0x08:
                    return ("MIFARE 1K");
                case 0x09:
                    return ("MIFARE MINI");
                case 0x18:
                    return ("MIFARE 4K");
                case 0x20:
                    return ("MIFARE DESFIRE ");
                case 0x28:
                    return ("JCOP30");
                case 0x98:
                    return ("Gemplus MPCOS");
                default:
                    return ("Unknown Card");

            }
        }
        #endregion
    }
}

