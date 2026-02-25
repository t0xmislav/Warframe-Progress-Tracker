using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Warframe_Progress_Tracker.Utils
{
    public static class AesKeyManager
    {
        private static readonly string KeyFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aes_key.dat");
        private static byte[]? _key;

        public static void Initialize()
        {
            if(_key != null) return;
            _key = LoadOrCreateKey();
        }

        public static byte[] GetKey()
        {
            if(_key == null) Initialize();
            return _key;
        }

        private static byte[] LoadOrCreateKey()
        {
            if(System.IO.File.Exists(KeyFile))
            {
                var protectedBytes = System.IO.File.ReadAllBytes(KeyFile);
                try
                {
                    return ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                }
                catch
                {
                    System.IO.File.Delete(KeyFile);
                }
            }
            var key = new byte[32];
            var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
            System.IO.File.WriteAllBytes(KeyFile, protectedKey);
            return key;
        }
    }
}
