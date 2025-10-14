using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warframe.Tracker.Filters;
using Warframe_Progress_Tracker.Model;
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
                    Platform VARCHAR(15)
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
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            var categoryId = AddCategory(item.Category.DisplayName);

            var command = connection.CreateCommand();
            command.CommandText =
                @"
                    INSERT OR IGNORE INTO Items (UniqueName, Name, CategoryId, Image, MasteryPoints)
                    VALUES ($uniqueName, $name, $categoryId, $image, $masteryPoints)";
            command.Parameters.AddWithValue("$uniqueName", item.UniqueName);
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$categoryId", categoryId);
            command.Parameters.AddWithValue("$image", item.Image ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$masteryPoints", MasteryCalculator.GetMasteryPoints(item.Category.DisplayName, item.UniqueName));

            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        public static int SaveItems(List<Item> items)
        {
            var count = 0;
            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            foreach (var item in items)
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Items WHERE UniqueName = $name";
                checkCmd.Parameters.AddWithValue("$name", item.UniqueName);

                long exists = (long)checkCmd.ExecuteScalar();

                if (exists == 0)
                {
                    AddItem(item);
                    count++;
                }
            }
            return count;
        }
        public static async Task<int> PopulateItemsFromApi(LoadingDialog dialog)
        {
            int newCount = 0;
            var progress = new Progress<string>(msg => dialog.UpdateMessage(msg));
            var items = await ApiService.FetchItemsAsync(progress);

            using var connection = new SqliteConnection($"Data source={dbPath}");
            connection.Open();

            foreach (var item in items)
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Items WHERE UniqueName = $name";
                checkCmd.Parameters.AddWithValue("$name", item.UniqueName);

                long exists = (long)checkCmd.ExecuteScalar();

                if (exists == 0)
                {
                    AddItem(item);
                    newCount++;
                }
            }
            return newCount;
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
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Nodes WHERE Name = $name";
                checkCmd.Parameters.AddWithValue("$name", node.Name);

                long exists = (long)checkCmd.ExecuteScalar();
                if (exists == 0)
                {
                    AddNode(node);
                    count++;
                }
            }
            return count;
        } 
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
        public static List<Item> GetAllItems()
        {
            var list = new List<Item>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT i.Id, i.UniqueName, i.Name, i.CategoryId, c.DisplayName, i.Image FROM items i
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
                    Category = new Category { Id = reader.GetInt32(3), DisplayName = reader.GetString(4) },
                    Image = reader.IsDBNull(5) ? null : (byte[])reader["Image"]
                };
                list.Add(item); 
            }
            return list;
        }
        public static List<Node> GetAllNodes()
        {
            var list = new List<Node>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT n.Id, n.Name, n.Planet, n.CategoryId, c.DisplayName, n.Image FROM nodes n
                LEFT JOIN Categories c ON n.CategoryId = c.Id;
            ";
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                var nodes = new Node
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Planet = reader.GetString(2),
                    Category = new Category { Id = reader.GetInt32(3), DisplayName = reader.GetString(4) },
                    Image = reader.IsDBNull(5) ? null : (byte[])reader["Image"]
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
        public static void UpdateItemProgress(int userId, int itemId, bool mastered, bool owned)
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
            cmd.ExecuteNonQuery();
        }
        public static void UpdateNodeProgress(int userId, int nodeId, bool cleared, bool clearedSteelPath)
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

        public static (List<Item> items, List<Node> nodes) GetAllCodexSummaries()
        {
            var items = GetAllItems();
            var nodes = GetAllNodes();
            return (items, nodes);
        }
    }
}
