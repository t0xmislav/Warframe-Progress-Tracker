using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;

namespace Warframe_Progress_Tracker.Services
{
    internal class AuthService
    {
        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static bool Register(string username, string password)
        {
            using var connection = new SqliteConnection($"Data Source={DbService.GetDbPath()}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Users (Username, PasswordHash)
                VALUES ($username, $passwordHash);";
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$passwordHash", HashPassword(password));

            try
            {
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (SqliteException e)
            {
                return false;
            }
        }

        public static User? Login(string username, string password)
        {
            using var connection = new SqliteConnection($"Data Source={DbService.GetDbPath()}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, PasswordHash FROM Users WHERE username = $username;";
            cmd.Parameters.AddWithValue("$username", username);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var storedHash = reader.GetString(2);
                if (storedHash == HashPassword(password))
                {
                    return new User
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        PasswordHash = storedHash
                    };
                }
            }
            return null;
        }
        //Attempts to link warframe account, but the api doesn't seem to recognize a lot of accounts, so it just sets the display name and platform.
        public static void LinkWarframeAccount(int userId, string displayName, string platform)
        {
            using var connection = new SqliteConnection($"Data Source={DbService.GetDbPath()}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users
                SET WarframeDisplayName = $displayName,
                    Platform = $platform
                WHERE Id = $id;
            ";
            cmd.Parameters.AddWithValue("$displayName", displayName);
            cmd.Parameters.AddWithValue("$platform", platform);
            cmd.Parameters.AddWithValue("$id", userId);

            cmd.ExecuteNonQuery();
        }
    }
}
