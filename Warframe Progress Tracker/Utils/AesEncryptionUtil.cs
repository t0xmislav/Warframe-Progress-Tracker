using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public static class AesEncryptionUtil
    {


        public static string Encrypt(string plainText)
        {
            var key = AesKeyManager.GetKey();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var cipherBytes = new byte[plainBytes.Length];

            using var aesGcm = new AesGcm(key, 32);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            using var ms = new System.IO.MemoryStream();
            ms.Write(nonce, 0, nonce.Length);
            ms.Write(tag, 0, tag.Length);
            ms.Write(cipherBytes, 0, cipherBytes.Length);
            return Convert.ToBase64String(ms.ToArray());

        }
    
        public static string Decrypt(string cipherText)
        {
            var payload = Convert.FromBase64String(cipherText);
            if(payload.Length < 28) throw new ArgumentException("Invalid cipher text");

            var nonce = new byte[12];
            var tag = new byte[16];
            Buffer.BlockCopy(payload, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(payload, nonce.Length, tag, 0, tag.Length);
            var cipherBytes = new byte[payload.Length - nonce.Length - tag.Length];
            Buffer.BlockCopy(payload, nonce.Length + tag.Length, cipherBytes, 0, cipherBytes.Length);

            var key = AesKeyManager.GetKey();
            var plainBytes = new byte[cipherBytes.Length];

            using var aesGcm = new AesGcm(key, 32);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
