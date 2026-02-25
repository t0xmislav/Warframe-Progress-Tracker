using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.Services
{
    internal class AuthService
    {
        private const int SaltSize = 32;
        private const int HashSize = 32;
        private const int Iterations = 200000;
        public static string HashPassword(string password)
        {
            var pepper = Environment.GetEnvironmentVariable("WPT_PEPPER") ?? "";

            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            using var kdf = new Rfc2898DeriveBytes(password + pepper, salt, Iterations, 
                HashAlgorithmName.SHA256);
            var hash = kdf.GetBytes(HashSize);

            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if(string.IsNullOrEmpty(storedHash)) return false;

            var pepper = Environment.GetEnvironmentVariable("WPT_PEPPER") ?? "";
            var parts = storedHash.Split('.');
            if (parts.Length != 3) return false;

            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);

            using var kdf = new Rfc2898DeriveBytes(password + pepper, salt, iterations, 
                HashAlgorithmName.SHA256);
            var actualHash = kdf.GetBytes(expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }

        public static bool Register(string username, string password, bool isAdmin = false)
        {
            if(DbService.IsUsernameTaken(username))
            {
                return false;
            }
            var hashedPassword = HashPassword(password);
            return DbService.AddUser(username, hashedPassword, isAdmin);
        }

        public static User? Login(string username, string password)
        {
            return DbService.Login(username, password);
        }
        //Attempts to link warframe account, but the api doesn't seem to recognize a lot of accounts, so it just sets the display name and platform.
        public static async Task<bool> LinkWarframeAccount(int userId, string displayName, string platform)
        {
            return await ApiService.FetchWarframeProfile(displayName, userId);
        }
    }
}
