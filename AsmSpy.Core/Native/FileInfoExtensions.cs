using System.IO;

namespace AsmSpy.Core.Native
{
    internal static class FileInfoExtensions
    {
        private const int BufferSize = 2048;

        internal static bool IsAssembly(this FileInfo fileInfo)
        {
            // Symbolic links always have a length of 0, which is the length of the symbolic link file (not the target file).
            // This check can be safely skipped.
            if (fileInfo.Length < BufferSize && !fileInfo.IsSymbolicLink())
            {
                return false;
            }

            var data = new byte[BufferSize];
            using (var fs = File.Open(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int iRead = fs.Read(data, 0, BufferSize);
                if (iRead != BufferSize)
                {
                    return false;
                }
            }

            unsafe
            {
                fixed (byte* pData = data)
                {
                    var pDosHeader = (ImageDosHeader*)pData;
                    var pNtHeader32 = (ImageNtHeaders32*)(pData + pDosHeader->FileAddressOfNewExeHeader);
                    var pNtHeader64 = (ImageNtHeaders64*)pNtHeader32;

                    // Prevent reading beyond the buffer
                    if (pNtHeader64 + 1 > pData + BufferSize)
                    {
                        return false;
                    }

                    ushort magic = pNtHeader32->OptionalHeader.Magic;

                    if (magic == 0x10b)
                    {
                        return pNtHeader32->OptionalHeader.NumberOfRvaAndSizes >= 15
                            && pNtHeader32->OptionalHeader.ComHeaderDirectory.VirtualAddress > 0;
                    }
                    if (magic == 0x20b)
                    {
                        return pNtHeader64->OptionalHeader.NumberOfRvaAndSizes >= 15
                            && pNtHeader64->OptionalHeader.ComHeaderDirectory.VirtualAddress > 0;
                    }
                    return false;
                }
            }
        }

        internal static bool IsSymbolicLink(this FileInfo fileInfo)
        {
            return fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }
}
