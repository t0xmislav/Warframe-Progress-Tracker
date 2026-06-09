using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public static class AesKeyManager
    {
        private static readonly string KeyFileName = "aes_key.dat";
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
            var keys = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys");
            Directory.CreateDirectory(keys);
            var keyFile = Path.Combine(keys, KeyFileName);
            if (File.Exists(keyFile))
            {
                var protectedBytes = File.ReadAllBytes(keyFile);
                try
                {
                    return ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                }
                catch
                {
                    File.Delete(keyFile);
                }
            }
            var key = new byte[32];
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                key = aes.Key;
            }
            var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyFile, protectedKey);
            return key;
        }
    }
}
