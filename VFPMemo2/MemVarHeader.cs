using System;
using System.Runtime.InteropServices;

namespace VFPMemo
{
    /// <summary>
    /// TMEMVarHeader = packed record
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct MemVarHeader
    {
        /// <summary>
        /// Nome da variável
        /// var_name: array[0..10] of AnsiChar;
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string var_name;

        /// <summary>
        /// Tipo
        /// mem_type: AnsiChar;               // 0ACDHLNOQYacdhlnoqy
        /// </summary>
        //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
        public char mem_type;

        /// <summary>
        /// big_size: UInt32;                 // only if mem_type == 'H'
        /// </summary>
        public uint big_size { get; private set; }

        /// <summary>
        /// width   : Byte;                   // special meaning if mem_type == 'H'
        /// </summary>
        public byte width { get; private set; }

        /// <summary>
        /// decimals: Byte;
        /// </summary>
        public byte decimals { get; private set; }

        /// <summary>
        /// padding : array[0..13] of Byte;  // 0 0 0 0 0 0 0 3 0 0 0 0 0 0
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public byte[] padding;


    }
}
