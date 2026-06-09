using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Utils
{
    public static class RsaUtil
    {
        private const string KeyFileName = "progress_rsa.key.prot";
        private const int RsaKeySize = 2048;

        public static async Task<(bool Success, string? Path)> SignPorgressSnapshot(string xml)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "XML Progress|*.xml",
                FileName = $"Progress-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xml"
            };
            if (dlg.ShowDialog() != true) return (false, null);

            var path = dlg.FileName;
            await File.WriteAllTextAsync(path, xml, Encoding.UTF8);

            using var rsa = LoadOrCreateRsaPrivateKey();
            var xmlBytes = Encoding.UTF8.GetBytes(xml);
            var signature = rsa.SignData(xmlBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            await File.WriteAllBytesAsync(path + ".sig", signature);

            var pub = rsa.ExportSubjectPublicKeyInfo();
            await File.WriteAllBytesAsync(path + ".pub", pub);

            return (true, path);
        }

        public static (bool Verified, string? Xml) VerifyProgressSnapshot(string xmlPath)
        {
            try
            {
                var sigPath = xmlPath + ".sig";
                Debug.WriteLine($"Sig path, " + sigPath);

                var pubPath = xmlPath + ".pub";

                Debug.WriteLine($"Pub path, " + pubPath);
                if (!File.Exists(sigPath) || !File.Exists(pubPath))
                {
                    Debug.WriteLine("File doesn't exist");
                    return (false, null);
                }
                Debug.WriteLine("Trying to read xml text");
                var xmlText = File.ReadAllText(xmlPath, Encoding.UTF8);
                Debug.WriteLine("Trying to read bytes");
                var xmlBytes = Encoding.UTF8.GetBytes(xmlText);
                var sigBytes = File.ReadAllBytes(sigPath);
                var pubBytes = File.ReadAllBytes(pubPath);

                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(pubBytes, out _);

                var ok = rsa.VerifyData(xmlBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                Debug.WriteLine($"Verification result: {ok}");
                return ok ? (true, Encoding.UTF8.GetString(xmlBytes)) : (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        
        // Load existing RSA private key from protected file or create and persist a new one.
        private static RSA LoadOrCreateRsaPrivateKey()
        {
            var keys = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys");
            Directory.CreateDirectory(keys);
            var keyFile = Path.Combine(keys, KeyFileName);

            if (File.Exists(keyFile))
            {
                try
                {
                    var protectedData = File.ReadAllBytes(keyFile);
                    var privateBytes = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
                    var rsa = RSA.Create();
                    rsa.ImportRSAPrivateKey(privateBytes, out _);
                    return rsa;
                }
                catch
                {
                }
            }

            using var rsaNew = RSA.Create(RsaKeySize);
            var privateKey = rsaNew.ExportRSAPrivateKey();
            var protectedPrivate = ProtectedData.Protect(privateKey, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyFile, protectedPrivate);

            var rsaOut = RSA.Create();
            rsaOut.ImportRSAPrivateKey(privateKey, out _);
            return rsaOut;
        }
    }
}
