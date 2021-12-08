using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace RawInputLight
{
    public  class RawInput
    {
        private void Foo()
        {
            using SafeHandle handle = PInvoke.CreateFile("text.txt", 
                FILE_ACCESS_FLAGS.FILE_ADD_FILE, 
                FILE_SHARE_FLAGS.FILE_SHARE_WRITE, 
                null,
                FILE_CREATE_FLAGS.CREATE_ALWAYS, 
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_ARCHIVE, 
                CloseHandleSafeHandle.Null);
        }
    }
}