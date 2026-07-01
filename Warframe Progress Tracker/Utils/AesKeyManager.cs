using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Utils.Logger;

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
            return _key!;
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
                catch(Exception ex)
                {
                    LoggerService.Log("KeyManager", $"Failed to unprotect existing key, regenerating: {ex.Message}");
                    File.Delete(keyFile);
                }
            }
            byte[] key;
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
