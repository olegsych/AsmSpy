using System;
using System.IO;
using System.Reflection;

namespace AsmSpy.Core
{
    internal static class FileInfoExtensions
    {
        // https://learn.microsoft.com/dotnet/standard/assembly/identify
        internal static bool IsAssembly(this FileInfo fileInfo)
        {
            try
            {
                AssemblyName.GetAssemblyName(fileInfo.FullName);
                // Yes, the file is an assembly
            }
            catch (BadImageFormatException)
            {
                // The file is not an assembly
                return false;
            }
            catch (FileLoadException)
            {
                // The assembly has already been loaded
            }

            return true;
        }
    }
}
