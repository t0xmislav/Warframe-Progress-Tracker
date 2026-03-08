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

namespace CodexSnapshotPlugin
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

            var grid = new System.Windows.Controls.Grid
            {
                Margin = new Thickness(10)
            };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());

            var exportButton = new System.Windows.Controls.Button
            {
                Content = "Export Codex Snapshot",
                Margin = new Thickness(0, 0, 0, 10),
                Width = 140,
            };

            var importButton = new System.Windows.Controls.Button
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

            return window;
        }
        private static async Task ExportClickAsync(Window owner)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "XML Snapshot|*.xml",
                    FileName = $"CodexSnapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xml"
                };
                if (dlg.ShowDialog(owner) != true) return;
                var path = dlg.FileName;

                // Collect codex data via reflection: call DbService.GetAllItems & GetAllNodes
                var (items, nodes) = GetCodexSummariesViaReflection();

                // Build XML
                var doc = new XDocument(new XElement("CodexSnapshot",
                    new XAttribute("Generated", DateTime.UtcNow.ToString("o")),
                    new XElement("Items",
                        items.Select(it =>
                            new XElement("Item",
                                new XAttribute("UniqueName", (string)SafeGetProp(it, "UniqueName") ?? ""),
                                new XAttribute("Name", (string)SafeGetProp(it, "Name") ?? ""),
                                new XAttribute("Category", ((object)SafeGetProp(it, "Category") is object cat) ? (string)SafeGetProp(SafeGetProp(it, "Category"), "DisplayName") ?? "" : ""),
                                new XAttribute("MasteryPoints", SafeGetProp(it, "MasteryPoints")?.ToString() ?? "0")
                            )
                        )
                    ),
                    new XElement("Nodes",
                        nodes.Select(n =>
                            new XElement("Node",
                                new XAttribute("Name", (string)SafeGetProp(n, "Name") ?? ""),
                                new XAttribute("Planet", (string)SafeGetProp(n, "Planet") ?? ""),
                                new XAttribute("MasteryPoints", SafeGetProp(n, "MasteryPoints")?.ToString() ?? "0")
                            )
                        )
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

                MessageBox.Show(owner, $"Snapshot saved to:\n{path}\nSignature: {sigPath}\nPublic key: {pubPath}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
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

                // Ask for public key if not found
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

                // Optional: load data into DB (reflective)
                var doc = XDocument.Load(path);
                var items = doc.Root.Element("Items")?.Elements("Item") ?? Enumerable.Empty<XElement>();
                var nodes = doc.Root.Element("Nodes")?.Elements("Node") ?? Enumerable.Empty<XElement>();

                // Ask user if they want to import
                var res = MessageBox.Show(owner, $"Snapshot signature OK.\nItems: {items.Count()} Nodes: {nodes.Count()}\nImport into database?", "Import", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                // Build object lists and call DbService SaveItems/SaveNodes via reflection
                // We will construct minimal POCOs by using host's Item/Node types if available, else skip.
                var (itemList, nodeList) = BuildObjectsForImport(items, nodes);

                // Use reflection to call DbService.SaveItems and SaveNodes if present
                var dbType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Services.DbService");
                if (dbType != null)
                {
                    var saveItemsMethod = dbType.GetMethod("SaveItems", BindingFlags.Public | BindingFlags.Static);
                    var saveNodesMethod = dbType.GetMethod("SaveNodes", BindingFlags.Public | BindingFlags.Static);
                    if (saveItemsMethod != null && itemList != null && itemList.Count > 0)
                    {
                        saveItemsMethod.Invoke(null, new object[] { itemList });
                    }
                    if (saveNodesMethod != null && nodeList != null && nodeList.Count > 0)
                    {
                        saveNodesMethod.Invoke(null, new object[] { nodeList });
                    }
                    MessageBox.Show(owner, "Imported snapshot into database (if host methods found).", "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(owner, "Host DbService not found. Import skipped.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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

            var items = getItems != null ? (getItems.Invoke(null, null) as System.Collections.IEnumerable) : null;
            var nodes = getNodes != null ? (getNodes.Invoke(null, null) as System.Collections.IEnumerable) : null;

            return (items?.Cast<object>().ToArray() ?? Array.Empty<object>(), nodes?.Cast<object>().ToArray() ?? Array.Empty<object>());
        }

        // Build minimal host-compatible object lists for SaveItems/SaveNodes invoke
        private static (IList itemList, IList nodeList) BuildObjectsForImport(IEnumerable<XElement> itemsXml, IEnumerable<XElement> nodesXml)
        {
            // Try to locate host Item and Node types
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            }).ToArray();

            var itemType = types.FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Model.Item");
            var categoryType = types.FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Model.Category");
            var nodeType = types.FirstOrDefault(t => t.FullName == "Warframe_Progress_Tracker.Model.Node");

            IList itemList = null;
            IList nodeList = null;

            if (itemType != null && categoryType != null)
            {
                var listType = typeof(List<>).MakeGenericType(itemType);
                itemList = (IList)Activator.CreateInstance(listType)!;
                foreach (var xe in itemsXml)
                {
                    var instance = Activator.CreateInstance(itemType)!;
                    SetProp(instance, "Name", (string?)xe.Attribute("Name") ?? "");
                    SetProp(instance, "UniqueName", (string?)xe.Attribute("UniqueName") ?? "");
                    SetProp(instance, "MasteryPoints", int.Parse((string?)xe.Attribute("MasteryPoints") ?? "0"));
                    var categoryInstance = Activator.CreateInstance(categoryType)!;
                    SetProp(categoryInstance, "DisplayName", (string?)xe.Attribute("Category") ?? "");
                    SetProp(instance, "Category", categoryInstance);
                    itemList.Add(instance);
                }
            }

            if (nodeType != null)
            {
                var listType = typeof(List<>).MakeGenericType(nodeType);
                nodeList = (IList)Activator.CreateInstance(listType)!;
                foreach (var xe in nodesXml)
                {
                    var instance = Activator.CreateInstance(nodeType)!;
                    SetProp(instance, "Name", (string?)xe.Attribute("Name") ?? "");
                    SetProp(instance, "Planet", (string?)xe.Attribute("Planet") ?? "");
                    SetProp(instance, "MasteryPoints", int.Parse((string?)xe.Attribute("MasteryPoints") ?? "0"));
                    nodeList.Add(instance);
                }
            }

            return (itemList ?? new ArrayList(), nodeList ?? new ArrayList());
        }

        private static object SafeGetProp(object obj, string prop)
        {
            var pi = obj.GetType().GetProperty(prop);
            return pi?.GetValue(obj) ?? "";
        }

        private static void SetProp(object obj, string prop, object value)
        {
            var pi = obj.GetType().GetProperty(prop);
            if (pi != null && pi.CanWrite) pi.SetValue(obj, Convert.ChangeType(value, pi.PropertyType));
        }

        // RSA key storage: store DPAPI-protected private key bytes in plugin folder
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