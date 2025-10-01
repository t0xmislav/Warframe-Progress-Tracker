using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;

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
                    ImageUrl VARCHAR(255),
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
                    INSERT OR IGNORE INTO Items (UniqueName, Name, CategoryId, ImageUrl)
                    VALUES ($uniqueName, $name, $categoryId, $imageUrl)";
            command.Parameters.AddWithValue("$uniqueName", item.UniqueName);
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$categoryId", categoryId);
            command.Parameters.AddWithValue("$imageUrl", item.ImageUrl);

            int rowsAffected = command.ExecuteNonQuery();
            Console.WriteLine("Item Added");
            return rowsAffected > 0;
        }
        public static async Task<int> PopulateItemsFromApi()
        {
            int newCount = 0;

            var items = await ApiService.FetchItemsAsync();

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
        public static List<Item> GetAllItems()
        {
            var list = new List<Item>();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT i.Id, i.UniqueName, i.Name, i.CategoryId, c.DisplayName, i.ImageUrl FROM items i
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
                    ImageUrl = reader.GetString(5)
                };
                list.Add(item); 
            }
            return list;
        }
        public static UserProgress GetProgressForItem(int userId, int itemId) {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Owned, Mastered, DateOwned, DateMastered
                FROM UserProgress
                WHERE UserId = $userId AND ItemId = $itemId;
            ";
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$itemId", itemId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new UserProgress
                {
                    UserId = userId,
                    ItemId = itemId,
                    Owned = reader.GetInt32(0) == 1,
                    Mastered = reader.GetInt32(1) == 1,
                    DateOwned = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    DateMastered = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3))
                };
            }
            return new UserProgress { ItemId = itemId, UserId = userId };
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
        public static void SetOwned(int userId, int itemId, bool owned)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UserProgress (UserId, ItemId, Owned, DateOwned)
                VALUES ($userId, $itemId, $owned, $date)
                ON CONFLICT(UserId, ItemId) DO UPDATE SET
                    Owned = $owned,
                    DateOwned = CASE WHEN $owned=1 THEN $date ELSE NULL END
                ";
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$itemId", itemId);
            cmd.Parameters.AddWithValue("$owned", owned ? 1 : 0);
            cmd.Parameters.AddWithValue("$date", owned ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        public static void UpdateProgress(int userId, int itemId, bool mastered, bool owned)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UserProgress (UserId, ItemId, Owned, Mastered, DateOwned, DateMastered)
                VALUES ($userId, $itemId, $owned, $mastered, $ownedDate, $masteredDate)
                ON CONFLICT(UserId, ItemId) DO UPDATE SET
                    Owned = $owned,
                    Mastered = $mastered,
                    DateOwned = $ownedDate,
                    DateMastered = $masteredDate;
                ";
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$itemId", itemId);
            cmd.Parameters.AddWithValue("$owned", owned ? 1 : 0);
            cmd.Parameters.AddWithValue("$mastered", mastered ? 1 : 0);
            cmd.Parameters.AddWithValue("$ownedDate", owned ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$masteredDate", mastered ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        public static void SaveProgress(ItemWithProgress vm, User user)
        {
            if (vm == null) return;

            using var connection = new SqliteConnection($"Data Source={DbService.GetDbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UserProgress (UserId, ItemId, Owned, Mastered, DateOwned, DateMastered)
                VALUES ($userId, $itemId, $owned, $mastered, $ownedDate, $masteredDate)
                ON CONFLICT(UserId, ItemId) DO UPDATE SET
                    Owned=$owned, Mastered=$mastered,
                    DateOwned=$ownedDate, DateMastered=$masteredDate;
                ";
            cmd.Parameters.AddWithValue("$userId", user.Id);
            cmd.Parameters.AddWithValue("$itemId", vm.Item.Id);
            cmd.Parameters.AddWithValue("$owned", vm.Owned ? 1 : 0);
            cmd.Parameters.AddWithValue("$mastered", vm.Mastered ? 1 : 0);
            cmd.Parameters.AddWithValue("$ownedDate", vm.Owned ? vm.DateOwned?.ToString("o") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$masteredDate", vm.Mastered ? vm.DateMastered?.ToString("o") : (object)DBNull.Value);

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
    }
}
