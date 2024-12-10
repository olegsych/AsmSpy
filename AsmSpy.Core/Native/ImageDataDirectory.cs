using System.Runtime.InteropServices;

namespace AsmSpy.Core.Native
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct ImageDataDirectory
    {
        [FieldOffset(0)]
        public uint VirtualAddress;
        [FieldOffset(4)]
        public uint Size;
    }
}