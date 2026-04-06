using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

        // Shows SaveFileDialog, writes xml, signs it and writes .sig and .pub files.
        // Returns (success, savedPath)
        public static async Task<(bool Success, string? Path)> ExportProgressWithSignature(string xml)
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

        // Verifies xmlPath by checking xmlPath.sig and xmlPath.pub (or supplied pubKeyPath).
        // Returns (verified, xmlContentIfVerified)
        public static (bool Verified, string? Xml) VerifyProgressFile(string xmlPath, string? pubKeyPath = null)
        {
            try
            {
                var sigPath = xmlPath + ".sig";
                var pubPath = pubKeyPath ?? (xmlPath + ".pub");
                if (!File.Exists(sigPath) || !File.Exists(pubPath)) return (false, null);

                var xmlBytes = File.ReadAllBytes(xmlPath);
                var sigBytes = File.ReadAllBytes(sigPath);
                var pubBytes = File.ReadAllBytes(pubPath);

                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(pubBytes, out _);

                var ok = rsa.VerifyData(xmlBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return ok ? (true, Encoding.UTF8.GetString(xmlBytes)) : (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        public static bool ApplyProgressSnapshot(string xmlContent)
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var root = doc.Root;
                if (root is null) return false;
                var userId = root.Element("User")?.Attribute("Id")?.Value;
                if (userId == null) return false;
                var itemProgresses = root.Element("ItemProgress")?.Elements("Item");
                foreach (var itemProgress in itemProgresses ?? Enumerable.Empty<XElement>())
                {
                    var id = itemProgress.Attribute("Id")?.Value;
                    var owned = itemProgress.Attribute("Owned")?.Value == "1";
                    var mastered = itemProgress.Attribute("Mastered")?.Value == "1";
                    var dateOwned = DateTime.TryParse(itemProgress.Attribute("DateOwned")?.Value, out var dow) ? dow : (DateTime?)null;
                    var dateMastered = DateTime.TryParse(itemProgress.Attribute("DateMastered")?.Value, out var dm) ? dm : (DateTime?)null;
                    if (id is not null)
                    {
                        DbService.SetItemProgress(int.Parse(userId), int.Parse(id), owned, mastered, dateOwned, dateMastered);
                    }
                }
                var nodeProgresses = root.Element("NodeProgress")?.Elements("Node");
                foreach (var nodeProgress in nodeProgresses ?? Enumerable.Empty<XElement>())
                {
                    var id = nodeProgress.Attribute("Id")?.Value;
                    var cleared = nodeProgress.Attribute("Cleared")?.Value == "1";
                    var clearedSteelPath = nodeProgress.Attribute("ClearedSteelPath")?.Value == "1";
                    var dateNormalCleared = DateTime.TryParse(nodeProgress.Attribute("DateNormalCleared")?.Value, out var dnc) ? dnc : (DateTime?)null;
                    var dateSteelPathCleared = DateTime.TryParse(nodeProgress.Attribute("DateSteelPathCleared")?.Value, out var dspc) ? dspc : (DateTime?)null;
                    if (id is not null)
                    {
                        DbService.SetNodeProgress(int.Parse(userId), int.Parse(id), cleared, clearedSteelPath, dateNormalCleared, dateSteelPathCleared);
                    }

                }
                return true;
            }
            catch
            {
                LoggerService.Log("Failed to apply progress snapshot", "Saving progress snapshot to database failed");
                return false;
            }
        }
        // Load existing RSA private key from protected file or create and persist a new one.
        private static RSA LoadOrCreateRsaPrivateKey()
        {
            var plugins = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            Directory.CreateDirectory(plugins);
            var keyFile = Path.Combine(plugins, KeyFileName);

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
                    // fall through and recreate key
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
