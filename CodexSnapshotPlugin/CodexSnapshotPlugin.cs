using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Warframe.Tracker.CodexSnapshotPlugin
{
    public static class CodexSnapshotPlugin
    {
        public static Window CreateDialog()
        {
            var window = new Window
            {
                Title = "Codex Snapshot - Import / Export",
                Width = 520,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };

            var grid = new Grid
            {
                Margin = new Thickness(10)
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());

            var exportButton = new Button
            {
                Content = "Export Codex Snapshot",
                Margin = new Thickness(0, 0, 0, 10),
                Width = 140,
            };

            var importButton = new Button
            {
                Content = "Import Codex Snapshot",
                Margin = new Thickness(0, 0, 0, 10),
                Width = 140,
            };
            var info = new TextBlock
            {
                Text = "Export creates XML + .sig + .pub (public key). Import verifies signature.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            panel.Children.Add(exportButton);
            panel.Children.Add(importButton);
            Grid.SetRow(panel, 0);
            grid.Children.Add(panel);

            Grid.SetRow(info, 1);
            grid.Children.Add(info);

            exportButton.Click += async (_, __) => await ExportClickAsync(window);
            importButton.Click += async (_, __) => await ImportClickAsync(window);
            window.Content = grid;
            return window;
        }
        private static async Task ExportClickAsync(Window owner)
        {
            try
            {
                // Determine logged-in user from host (owner.Owner)
                var hostUser = TryGetLoggedInUser(owner);
                if (hostUser == null)
                {
                    MessageBox.Show(owner, "Could not determine logged-in user from host. Open plugin from the main window while logged in.", "User not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var userIdProp = hostUser.GetType().GetProperty("Id");
                var userNameProp = hostUser.GetType().GetProperty("Name");
                var userId = (int)(userIdProp?.GetValue(hostUser) ?? 0);
                var userName = (string?)(userNameProp?.GetValue(hostUser) ?? "(unknown)");

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "XML Snapshot|*.xml",
                    FileName = $"CodexProgress-{userName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xml"
                };
                if (dlg.ShowDialog(owner) != true) return;
                var path = dlg.FileName;

                // Get items & nodes from host
                var (items, nodes) = GetCodexSummariesViaReflection();

                // Build progress entries by calling DbService.GetProgressForItem and GetProgressForNode via reflection
                var asmTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                });
                var dbType = asmTypes.FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Services.DbService");
                if (dbType == null)
                {
                    MessageBox.Show(owner, "Host DbService not found. Cannot export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var getProgressItem = dbType.GetMethod("GetProgressForItem", BindingFlags.Public | BindingFlags.Static);
                var getProgressNode = dbType.GetMethod("GetProgressForNode", BindingFlags.Public | BindingFlags.Static);

                var doc = new XDocument(new XElement("CodexProgressSnapshot",
                    new XAttribute("Generated", DateTime.UtcNow.ToString("o")),
                    new XElement("User",
                        new XAttribute("Id", userId),
                        new XAttribute("Name", userName)
                    ),
                    new XElement("ItemProgress",
                        items.Select(it =>
                        {
                            try
                            {
                                var progress = getProgressItem?.Invoke(null, new object[] { hostUser, it });
                                // Extract properties from progress object via reflection
                                var owned = GetBoolProp(progress, "Owned");
                                var mastered = GetBoolProp(progress, "Mastered");
                                var dateOwned = GetDateTimeProp(progress, "DateOwned");
                                var dateMastered = GetDateTimeProp(progress, "DateMastered");
                                var unique = (string?)SafeGetProp(it, "UniqueName") ?? "";
                                return new XElement("Item",
                                    new XAttribute("UniqueName", unique),
                                    new XAttribute("Owned", owned ? "1" : "0"),
                                    new XAttribute("Mastered", mastered ? "1" : "0"),
                                    new XAttribute("DateOwned", dateOwned?.ToString("o") ?? ""),
                                    new XAttribute("DateMastered", dateMastered?.ToString("o") ?? "")
                                );
                            }
                            catch { return null; }
                        }).Where(x => x != null)
                    ),
                    new XElement("NodeProgress",
                        nodes.Select(n =>
                        {
                            try
                            {
                                var progress = getProgressNode?.Invoke(null, new object[] { hostUser, n });
                                var clearedNormal = GetBoolProp(progress, "ClearedNormal");
                                var clearedSP = GetBoolProp(progress, "ClearedSteelPath");
                                var dateNormal = GetDateTimeProp(progress, "DateNormalClear");
                                var dateSP = GetDateTimeProp(progress, "DateSteelPathClear");
                                var nodeName = (string?)SafeGetProp(n, "Name") ?? "";
                                return new XElement("Node",
                                    new XAttribute("Name", nodeName),
                                    new XAttribute("ClearedNormal", clearedNormal ? "1" : "0"),
                                    new XAttribute("ClearedSteelPath", clearedSP ? "1" : "0"),
                                    new XAttribute("DateNormalClear", dateNormal?.ToString("o") ?? ""),
                                    new XAttribute("DateSteelPathClear", dateSP?.ToString("o") ?? "")
                                );
                            }
                            catch { return null; }
                        }).Where(x => x != null)
                    )
                ));

                doc.Save(path);

                // Sign XML
                var xmlBytes = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
                var privateKey = LoadOrCreateRsaPrivateKey();
                var signature = privateKey.SignData(xmlBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var sigPath = path + ".sig";
                await File.WriteAllBytesAsync(sigPath, signature);

                // Export the public key for verification by others
                var pubBytes = privateKey.ExportSubjectPublicKeyInfo();
                var pubPath = path + ".pub";
                await File.WriteAllBytesAsync(pubPath, pubBytes);

                MessageBox.Show(owner, $"Progress snapshot saved for {userName}:\n{path}\nSignature: {sigPath}\nPublic key: {pubPath}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static async Task ImportClickAsync(Window owner)
        {
            try
            {
                var hostUser = TryGetLoggedInUser(owner);
                if (hostUser == null)
                {
                    MessageBox.Show(owner, "Could not determine logged-in user from host. Open plugin from the main window while logged in.", "User not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var userId = (int)(hostUser.GetType().GetProperty("Id")?.GetValue(hostUser) ?? 0);
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "XML Snapshot|*.xml",
                    Multiselect = false
                };
                if (dlg.ShowDialog(owner) != true) return;
                var path = dlg.FileName;
                var sigPath = path + ".sig";
                var pubPath = path + ".pub";

                if (!File.Exists(sigPath))
                {
                    MessageBox.Show(owner, $"Signature file not found: {sigPath}", "Missing signature", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!File.Exists(pubPath))
                {
                    var ask = MessageBox.Show(owner, "Public key not found next to snapshot. Select public key file?", "Public key", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (ask == MessageBoxResult.Yes)
                    {
                        var keyDlg = new Microsoft.Win32.OpenFileDialog { Filter = "Public key|*.pub;*.der" };
                        if (keyDlg.ShowDialog(owner) != true) return;
                        pubPath = keyDlg.FileName;
                    }
                    else
                    {
                        return;
                    }
                }

                var xmlBytes = await File.ReadAllBytesAsync(path);
                var sigBytes = await File.ReadAllBytesAsync(sigPath);
                var pubBytes = await File.ReadAllBytesAsync(pubPath);

                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(pubBytes, out _);

                var ok = rsa.VerifyData(xmlBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!ok)
                {
                    MessageBox.Show(owner, "Signature verification failed. Import aborted.", "Signature error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var doc = XDocument.Load(path);
                var itemsXml = doc.Root.Element("ItemProgress")?.Elements("Item") ?? Enumerable.Empty<XElement>();
                var nodesXml = doc.Root.Element("NodeProgress")?.Elements("Node") ?? Enumerable.Empty<XElement>();

                // Retrieve items and nodes to map unique names / names to ids
                var (items, nodes) = GetCodexSummariesViaReflection();
                var itemByUnique = items.ToDictionary(it => (string?)SafeGetProp(it, "UniqueName") ?? "", it => it);
                var nodeByName = nodes.ToDictionary(n => (string?)SafeGetProp(n, "Name") ?? "", n => n);

                // Reflection helpers for DbService update methods
                var asmTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                });
                var dbType = asmTypes.FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Services.DbService");
                if (dbType == null)
                {
                    MessageBox.Show(owner, "Host DbService not found. Cannot import.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var updateItemMethod = dbType.GetMethod("UpdateItemProgress", BindingFlags.Public | BindingFlags.Static);
                var updateNodeMethod = dbType.GetMethod("UpdateNodeProgress", BindingFlags.Public | BindingFlags.Static);
                int appliedItems = 0, appliedNodes = 0;

                // Apply item progress
                foreach (var xe in itemsXml)
                {
                    var unique = (string?)xe.Attribute("UniqueName") ?? "";
                    if (!itemByUnique.TryGetValue(unique, out var itemObj)) continue;
                    var itemId = (int)(itemObj.GetType().GetProperty("Id")?.GetValue(itemObj) ?? 0);
                    var owned = ((string?)xe.Attribute("Owned") ?? "0") == "1";
                    var mastered = ((string?)xe.Attribute("Mastered") ?? "0") == "1";
                    // call UpdateItemProgress(userId, itemId, mastered, owned)
                    try
                    {
                        updateItemMethod?.Invoke(null, new object[] { userId, itemId, mastered, owned });
                        appliedItems++;
                    }
                    catch { /* ignore per-row errors */ }
                }

                // Apply node progress
                foreach (var xe in nodesXml)
                {
                    var name = (string?)xe.Attribute("Name") ?? "";
                    if (!nodeByName.TryGetValue(name, out var nodeObj)) continue;
                    var nodeId = (int)(nodeObj.GetType().GetProperty("Id")?.GetValue(nodeObj) ?? 0);
                    var clearedNormal = ((string?)xe.Attribute("ClearedNormal") ?? "0") == "1";
                    var clearedSP = ((string?)xe.Attribute("ClearedSteelPath") ?? "0") == "1";
                    // call UpdateNodeProgress(userId, nodeId, clearedNormal, clearedSP)
                    try
                    {
                        updateNodeMethod?.Invoke(null, new object[] { userId, nodeId, clearedNormal, clearedSP });
                        appliedNodes++;
                    }
                    catch { }
                }

                MessageBox.Show(owner, $"Import complete. Applied item progress: {appliedItems}, node progress: {appliedNodes}", "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Helper: reflection to call host DbService.GetAllItems and GetAllNodes
        private static (object[] items, object[] nodes) GetCodexSummariesViaReflection()
        {
            var asmTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
            {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            });

            var dbType = asmTypes.FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Services.DbService");
            if (dbType == null) return (Array.Empty<object>(), Array.Empty<object>());

            var getItems = dbType.GetMethod("GetAllItems", BindingFlags.Public | BindingFlags.Static);
            var getNodes = dbType.GetMethod("GetAllNodes", BindingFlags.Public | BindingFlags.Static);

            var items = getItems != null ? (getItems.Invoke(null, null) as IEnumerable) : null;
            var nodes = getNodes != null ? (getNodes.Invoke(null, null) as IEnumerable) : null;

            return (items?.Cast<object>().ToArray() ?? Array.Empty<object>(), nodes?.Cast<object>().ToArray() ?? Array.Empty<object>());
        }

        private static object? TryGetLoggedInUser(Window pluginWindow)
        {
            try
            {
                var host = pluginWindow?.Owner;
                if (host == null) return null;

                // Try non-public field _currentUser (MainWindow implementation)
                var field = host.GetType().GetField("_currentUser", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(host);
                    if (val != null) return val;
                }

                // Try public property CurrentUser
                var prop = host.GetType().GetProperty("CurrentUser", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                {
                    var val = prop.GetValue(host);
                    if (val != null) return val;
                }

            }
            catch { }
            return null;
        }
        private static object SafeGetProp(object obj, string prop)
        {
            var pi = obj.GetType().GetProperty(prop);
            return pi?.GetValue(obj) ?? "";
        }
        private static bool GetBoolProp(object? obj, string prop)
        {
            if (obj == null) return false;
            var pi = obj.GetType().GetProperty(prop);
            if (pi == null) return false;
            var val = pi.GetValue(obj);
            if (val is bool b) return b;
            if (val is int i) return i != 0;
            return false;
        }
        private static DateTime? GetDateTimeProp(object? obj, string prop)
        {
            if (obj == null) return null;
            var pi = obj.GetType().GetProperty(prop);
            if (pi == null) return null;
            var val = pi.GetValue(obj);
            if (val is DateTime dt) return dt;
            if (val is string s && DateTime.TryParse(s, out var parsed)) return parsed;
            return null;
        }
        private static void SetProp(object obj, string prop, object value)
        {
            var pi = obj.GetType().GetProperty(prop);
            if (pi != null && pi.CanWrite) pi.SetValue(obj, Convert.ChangeType(value, pi.PropertyType));
        }

        private static RSA LoadOrCreateRsaPrivateKey()
        {
            var plugins = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            Directory.CreateDirectory(plugins);
            var keyFile = Path.Combine(plugins, "snapshot_rsa.key.prot");

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

            using var rsaNew = RSA.Create(2048);
            var privateKey = rsaNew.ExportRSAPrivateKey();
            var protectedPrivate = ProtectedData.Protect(privateKey, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyFile, protectedPrivate);

            var rsaOut = RSA.Create();
            rsaOut.ImportRSAPrivateKey(privateKey, out _);
            return rsaOut;
        }
    }

}