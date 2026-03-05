using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.View;

namespace Warframe_Progress_Tracker.Services
{
    class DbService
    {
        private static readonly string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WarframeTracker");
        private static readonly string dbPath = Path.Combine(folderPath, "warframeItemsDb.db");

        public static string GetDbPath() { return dbPath; }
        public static void InitializeDatabase() {
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Categories(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DisplayName VARCHAR(255) UNIQUE
                );
                CREATE TABLE IF NOT EXISTS Items (
                    Id Integer PRIMARY KEY AUTOINCREMENT,
                    UniqueName VARCHAR(255) Unique,
                    Name VARCHAR(255),
                    CategoryId Integer,
                    Image BLOB,
                    MasteryPoints INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(CategoryId) REFERENCES Categories(Id)
                );
                CREATE TABLE IF NOT EXISTS Nodes (
                    Id Integer PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Planet TEXT NOT NULL,
                    CategoryId Integer,
                    Image BLOB,
                    MasteryPoints INTEGER DEFAULT 0,
                    FOREIGN KEY(CategoryId) REFERENCES Categories(Id)
                );
                CREATE TABLE IF NOT EXISTS Users(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username VARCHAR(30) UNIQUE NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    WarframeDisplayName VARCHAR(30),
                    WarframeAccountId TEXT,
                    Platform VARCHAR(15),
                    IsAdmin INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS UserProgress(
                    ItemId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    Owned INTEGER NOT NULL DEFAULT 0,
                    Mastered INTEGER NOT NULL DEFAULT 0,
                    DateOwned TEXT,
                    DateMastered TEXT,
                    PRIMARY KEY(UserId, ItemId),
                    FOREIGN KEY(ItemId) REFERENCES Items(Id),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );
                CREATE TABLE IF NOT EXISTS NodeProgress(
                    NodeId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    ClearedNormal INTEGER NOT NULL DEFAULT 0,
                    ClearedSteelPath INTEGER NOT NULL DEFAULT 0,
                    DateNormalClear TEXT,
                    DateSteelPathClear TEXT,
                    PRIMARY KEY(UserId, NodeId),
                    FOREIGN KEY(UserId) REFERENCES Users(Id),
                    FOREIGN KEY(NodeId) REFERENCES Nodes(Id)
                );";
            command.ExecuteNonQuery();
        }

        public static int AddCategory(String categoryName)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var checkCommand = connection.CreateCommand();

            checkCommand.CommandText = "SELECT Id FROM Categories WHERE DisplayName = $name;";
            checkCommand.Parameters.AddWithValue("$name", categoryName);
            var result = checkCommand.ExecuteScalar();

            if(result != null) return Convert.ToInt32(result);

            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Categories (DisplayName) VALUES ($name);";
            insertCommand.Parameters.AddWithValue("$name", categoryName);
            insertCommand.ExecuteNonQuery();

            using var getIdCommand = connection.CreateCommand();
            getIdCommand.CommandText = "SELECT last_insert_rowId()";
            var newId = getIdCommand.ExecuteScalar();

            return Convert.ToInt32(newId);
            
        }
        public static bool AddItem(Item item)
        {
            var categoryId = AddCategory(item.Category.DisplayName);
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            
            var command = connection.CreateCommand();
            command.CommandText =
                @"
                    INSERT OR IGNORE INTO Items (UniqueName, Name, CategoryId, Image, MasteryPoints)
                    VALUES ($uniqueName, $name, $categoryId, $image, $masteryPoints)";
            System.Diagnostics.Debug.WriteLine($"Unique Name: {item.UniqueName}");
            System.Diagnostics.Debug.WriteLine($"Name: {item.Name}");
            System.Diagnostics.Debug.WriteLine($"CategoryId: {categoryId}");
            System.Diagnostics.Debug.WriteLine($"Image: {item.Image}");
            System.Diagnostics.Debug.WriteLine($"MasteryPoints: {item.MasteryPoints}");
            command.Parameters.AddWithValue("$uniqueName", item.UniqueName);
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$categoryId", categoryId);
            command.Parameters.AddWithValue("$image", item.Image ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$masteryPoints", item.MasteryPoints);

            var rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        public static int SaveItems(List<Item> items)
        {
            var count = 0;
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            foreach (var item in items)
            {
                AddItem(item);
                count++;
            }
            return count;
        }
        public static int SaveItemsBatch(List<Item> items)
        {
            if(items == null || items.Count == 0) return 0;
            int count = 0;

            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                using var getCategoryCmd = connection.CreateCommand();
                getCategoryCmd.CommandText = "SELECT Id FROM Categories WHERE DisplayName = $name;";
                var paramCatName = getCategoryCmd.Parameters.Add("$name", SqliteType.Text);

                using var insertCategoryCmd = connection.CreateCommand();
                insertCategoryCmd.CommandText = "INSERT INTO Categories (DisplayName) VALUES ($name);";
                var paramInsertCatName = insertCategoryCmd.Parameters.Add("$name", SqliteType.Text);

                using var insertItemCmd = connection.CreateCommand();
                insertItemCmd.CommandText =
                    @"
                    INSERT OR IGNORE INTO Items (UniqueName, Name, CategoryId, Image, MasteryPoints)
                    VALUES ($uniqueName, $name, $categoryId, $image, $masteryPoints)";
                var paramUniqueName = insertItemCmd.Parameters.Add("$uniqueName", SqliteType.Text);
                var paramName = insertItemCmd.Parameters.Add("$name", SqliteType.Text);
                var paramCategoryId = insertItemCmd.Parameters.Add("$categoryId", SqliteType.Integer);
                var paramImage = insertItemCmd.Parameters.Add("$image", SqliteType.Blob);
                var paramMasteryPoints = insertItemCmd.Parameters.Add("$masteryPoints", SqliteType.Integer);

                foreach(var item in items)
                {
                    paramCatName.Value = item.Category.DisplayName;
                    var catIdObj = getCategoryCmd.ExecuteScalar();
                    int categoryId;
                    if (catIdObj is null)
                    {
                        paramInsertCatName.Value = item.Category.DisplayName;
                        insertCategoryCmd.ExecuteNonQuery();
                        using var lastIdCmd = connection.CreateCommand();
                        lastIdCmd.CommandText = "SELECT last_insert_rowId()";
                        categoryId = Convert.ToInt32(lastIdCmd.ExecuteScalar());
                    }
                    else
                    {
                        categoryId = Convert.ToInt32(catIdObj);
                    }
                    paramUniqueName.Value = item.UniqueName ?? "";
                    paramName.Value = item.Name ?? "";
                    paramCategoryId.Value = categoryId;
                    paramImage.Value = item.Image ?? (object)DBNull.Value;
                    paramMasteryPoints.Value = item.MasteryPoints;

                    var rowsAffected = insertItemCmd.ExecuteNonQuery();
                    if (rowsAffected > 0) count += rowsAffected;
                }
                transaction.Commit();
            }catch
            {
                transaction.Rollback();
                throw;
            }

            return count;
        }
        public static bool ItemExists(string uniqueName)
        {
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Items WHERE UniqueName = $name";
            checkCmd.Parameters.AddWithValue("$name", uniqueName);

            long exists = (long)checkCmd.ExecuteScalar();
            return exists != 0;
        }
        public static bool NodeExists(string name)
        {
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Nodes WHERE Name = $name";
            checkCmd.Parameters.AddWithValue("$name", name);

            long exists = (long)checkCmd.ExecuteScalar();
            return exists != 0;
        }
        public static bool AddNode(Model.Node node)
        {
            var categoryId = AddCategory("Node");
            using var connection = new SqliteConnection($"Data source={dbPath}");

            connection.Open();


            var command = connection.CreateCommand();
            command.CommandText =
                @"
                    INSERT OR IGNORE INTO Nodes (Name, Planet, Image, MasteryPoints, CategoryId)
                    VALUES ($name, $planet, $image, $masteryPoints, $categoryId)";
            command.Parameters.AddWithValue("$name", node.Name);
            command.Parameters.AddWithValue("$planet", node.Planet);
            command.Parameters.AddWithValue("$categoryId", categoryId);
            command.Parameters.AddWithValue("$image", node.Image ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$masteryPoints", node.MasteryPoints);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        public static int SaveNodes(List<Node> nodes)
        {
            int count = 0;
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            foreach (var node in nodes)
            {
                AddNode(node);
                count++;
            }
            return count;
        }
        /*
        public static async Task<int> PopulateNodesFromWiki()
        {

            int newCount = 0;
            var nodes = await WikiScraperService.ScrapeNodesAsync();

            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            foreach (var node in nodes)
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Nodes WHERE Name = $name";
                checkCmd.Parameters.AddWithValue("$name", node.Name);

                long exists = (long)checkCmd.ExecuteScalar();
                if (exists == 0)
                {
                    AddNode(node);
                    newCount++;
                }
            }
            return newCount;

        }
        */
        public static List<Item> GetAllItems()
        {
            var list = new List<Item>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT i.Id, i.UniqueName, i.Name, i.MasteryPoints, i.CategoryId, c.DisplayName, i.Image FROM items i
                LEFT JOIN Categories c ON i.CategoryId = c.Id;
            ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var item = new Item
                {
                    Id = reader.GetInt32(0),
                    UniqueName = reader.GetString(1),
                    Name = reader.GetString(2),
                    MasteryPoints = reader.GetInt32(3),
                    Category = new Category { Id = reader.GetInt32(4), DisplayName = reader.GetString(5) },
                    Image = reader.IsDBNull(6) ? null : (byte[])reader["Image"]
                };
                list.Add(item); 
            }
            return list;
        }
        public static List<Item> GetItemByCategory(Category category)
        {
            var list = new List<Item>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT i.Id, i.UniqueName, i.Name, i.MasteryPoints, i.CategoryId, i.Image FROM items i
                WHERE i.CategoryId = $categoryId;
            ";
            command.Parameters.AddWithValue("$categoryId", category.Id);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var item = new Item
                {
                    Id = reader.GetInt32(0),
                    UniqueName = reader.GetString(1),
                    Name = reader.GetString(2),
                    MasteryPoints = reader.GetInt32(3),
                    Category = category,
                    Image = reader.IsDBNull(5) ? null : (byte[])reader["Image"]
                };
                list.Add(item);
            }
            return list;
        }
        public static bool IsUniqueNameTaken(string uniqueName)
        {
            var list = new List<Item>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM Items 
                WHERE UniqueName = $uniqueName;
            ";
            command.Parameters.AddWithValue("$uniqueName", uniqueName);
            var count = Convert.ToInt32(command.ExecuteScalar());
            return count > 0;

        }
        public static List<Node> GetAllNodes()
        {
            var list = new List<Node>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT n.Id, n.Name, n.Planet, n.MasteryPoints, n.CategoryId, c.DisplayName, n.Image FROM nodes n
                LEFT JOIN Categories c ON n.CategoryId = c.Id;
            ";
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                var nodes = new Node
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Planet = reader.GetString(2),
                    MasteryPoints = reader.GetInt32(3),
                    Category = new Category { Id = reader.GetInt32(4), DisplayName = reader.GetString(5) },
                    Image = reader.IsDBNull(6) ? null : (byte[])reader["Image"]
                };
                list.Add(nodes);
            }
            return list;
        }
        public static ItemProgress GetProgressForItem(User user, Item item) {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Owned, Mastered, DateOwned, DateMastered
                FROM UserProgress
                WHERE UserId = $userId AND ItemId = $nodeId;
            ";
            cmd.Parameters.AddWithValue("$userId", user.Id);
            cmd.Parameters.AddWithValue("$nodeId", item.Id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ItemProgress
                {
                    User = user,
                    Item = item,
                    Owned = reader.GetInt32(0) == 1,
                    Mastered = reader.GetInt32(1) == 1,
                    DateOwned = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    DateMastered = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3))
                };
            }
            return new ItemProgress { Item = item, User = user };
        }
        public static NodeProgress GetProgressForNode(User user, Node node)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ClearedNormal, ClearedSteelPath, DateNormalClear, DateSteelPathClear
                FROM NodeProgress
                WHERE UserId = $userId AND NodeId = $nodeId;
            ";
            cmd.Parameters.AddWithValue("$userId", user.Id);
            cmd.Parameters.AddWithValue("$nodeId", node.Id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new NodeProgress
                {
                    User = user,
                    Node = node,
                    ClearedNormal = reader.GetInt32(0) == 1,
                    ClearedSteelPath = reader.GetInt32(1) == 1,
                    DateNormalClear = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    DateSteelPathClear = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3))
                };
            }
            return new NodeProgress { Node = node, User = user };
        }
        public static List<Category> GetCategories()
        {
            var list = new List<Category>();
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.Id, c.DisplayName FROM Categories c;
            ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var category = new Category { Id = reader.GetInt32(0), DisplayName = reader.GetString(1) };
                list.Add(category);
            }
            return list;
        }
        public static HashSet<string> GetAllUniqueItemNames()
        {
            var set = new HashSet<string>();
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT i.UniqueName FROM Items i;
            ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                set.Add(reader.GetString(0));
            }
            return set;
        }
        public static bool UpdateItemProgress(int userId, int itemId, bool mastered, bool owned)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UserProgress (UserId, ItemId, Owned, Mastered, DateOwned, DateMastered)
                VALUES ($userId, $nodeId, $owned, $mastered, $ownedDate, $masteredDate)
                ON CONFLICT(UserId, ItemId) DO UPDATE SET
                    Owned = $owned,
                    Mastered = $mastered,
                    DateOwned = $ownedDate,
                    DateMastered = $masteredDate;
                
                ";
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$nodeId", itemId);
            cmd.Parameters.AddWithValue("$owned", owned ? 1 : 0);
            cmd.Parameters.AddWithValue("$mastered", mastered ? 1 : 0);
            cmd.Parameters.AddWithValue("$ownedDate", owned ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$masteredDate", mastered ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        public static bool UpdateNodeProgress(int userId, int nodeId, bool cleared, bool clearedSteelPath)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO NodeProgress (UserId, NodeId, ClearedNormal, ClearedSteelPath, DateNormalClear, DateSteelPathClear)
                VALUES ($userId, $nodeId, $cleared, $clearedSteelPath, $dateNormalClear, $dateSteelPathClear)
                ON CONFLICT(UserId, NodeId) DO UPDATE SET
                    ClearedNormal = $cleared,
                    ClearedSteelPath = $clearedSteelPath,
                    DateNormalClear = $dateNormalClear,
                    DateSteelPathClear = $dateSteelPathClear;
                ";
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$nodeId", nodeId);
            cmd.Parameters.AddWithValue("$clearedSteelPath", clearedSteelPath ? 1 : 0);
            cmd.Parameters.AddWithValue("$cleared", cleared ? 1 : 0);
            cmd.Parameters.AddWithValue("$dateNormalClear", cleared ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$dateSteelPathClear", clearedSteelPath ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            int rowsAffected = cmd.ExecuteNonQuery();

            return rowsAffected > 0;
        }
        public static bool DeleteItem(Item item)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM Items WHERE Id = $id;
            ";
            cmd.Parameters.AddWithValue("$id", item.Id);
            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }

        public static void DeleteNode(Node node)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM Nodes WHERE Id = $id;
            ";

            cmd.Parameters.AddWithValue("$id", node.Id);
            cmd.ExecuteNonQuery();
        }
        public static bool IsItemsTableEmpty()
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Items;";

            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count == 0;
        }
        public static bool UpdateNode(Node node)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Nodes
                SET Name = $name, Planet = $planet, MasteryPoints = $masteryPoints, Image = $image
                WHERE Id = $id;
            ";
            cmd.Parameters.AddWithValue("$id", node.Id);
            cmd.Parameters.AddWithValue("$name", node.Name);
            cmd.Parameters.AddWithValue("$planet", node.Planet);
            cmd.Parameters.AddWithValue("$masteryPoints", node.MasteryPoints);
            cmd.Parameters.AddWithValue("$image", node.Image ?? (object)DBNull.Value);
            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }

        public static bool UpdateItem(Item item)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Nodes
                SET Name = $name, MasteryPoints = $masteryPoints, CategoryId = $categoryId, Image = $image
                WHERE Id = $id;
            ";
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.Parameters.AddWithValue("$name", item.Name);
            cmd.Parameters.AddWithValue("$planet", item.Category.Id);
            cmd.Parameters.AddWithValue("$masteryPoints", item.MasteryPoints);
            cmd.Parameters.AddWithValue("$image", item.Image ?? (object)DBNull.Value);
            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        public static (List<Item> items, List<Node> nodes) GetAllCodexSummaries()
        {
            var items = GetAllItems();
            var nodes = GetAllNodes();
            return (items, nodes);
        }

        public static bool SetAdminStatus(int userId, bool isAdmin)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users
                SET IsAdmin = $isAdmin
                WHERE Id = $id;
            ";
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.Parameters.AddWithValue("$isAdmin", isAdmin ? 1 : 0);
            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }

        public static User? GetUserById(int userId)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, PasswordHash, WarframeDisplayName, WarframeAccountId, Platform, IsAdmin FROM Users WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", userId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                using (Aes myAes = Aes.Create())
                {
                    return new User
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        PasswordHash = reader.GetString(2),
                        WarframeDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                        WarframeAccountId = reader.IsDBNull(4) ? null : AesEncryptionUtil.Decrypt(reader.GetString(4)),
                        Platform = reader.IsDBNull(5) ? null : reader.GetString(5),
                        IsAdmin = reader.GetInt32(6) == 1
                    };
                }
            }
            return null;
        }
        public static bool AddUser(string username, string passwordHash, bool isAdmin)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
                @"
                    INSERT INTO Users (Username, PasswordHash, IsAdmin)
                    VALUES ($username, $passwordHash, $IsAdmin)";
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$passwordHash", passwordHash);
            command.Parameters.AddWithValue("$IsAdmin", isAdmin);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        public static bool IsUsernameTaken(string username)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = $username;";
            cmd.Parameters.AddWithValue("$username", username);
            long count = (long)cmd.ExecuteScalar();
            return count > 0;
        }
        public static User? Login(string username, string password)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, PasswordHash, WarframeDisplayName, " +
                "WarframeAccountId, Platform, IsAdmin FROM Users WHERE Username = $username";
            cmd.Parameters.AddWithValue("$username", username);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var passwordHash = reader.GetString(2);
                if (!AuthService.VerifyPassword(password, passwordHash))
                    return null;
                return new User
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    WarframeDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    WarframeAccountId = reader.IsDBNull(4) ? null : AesEncryptionUtil.Decrypt(reader.GetString(4)),
                    Platform = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsAdmin = reader.GetInt32(6) == 1
                };
            }
            return null;
        }
        public static bool SetUserWfAccount(int userId, string displayName, string accountId, string platform)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users
                SET WarframeDisplayName = $displayName,
                    WarframeAccountId = $accountId,
                    Platform = $platform
                WHERE Id = $id;
            ";
            cmd.Parameters.AddWithValue("$displayName", displayName);
            cmd.Parameters.AddWithValue("$accountId", AesEncryptionUtil.Encrypt(accountId));
            cmd.Parameters.AddWithValue("$platform", platform);
            cmd.Parameters.AddWithValue("$id", userId);

            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }
    }
}
