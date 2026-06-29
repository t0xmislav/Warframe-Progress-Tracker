using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Services
{
    internal class AuthService
    {
        private const int HashSize = 32;
        private const int Iterations = 200000;

        private static byte[] DeriveSaltFromUsername(string username, string pepper)
        {
            var usernameBytes = Encoding.UTF8.GetBytes(username);
            if(string.IsNullOrEmpty(pepper))
            {
                throw new InvalidOperationException("Pepper is not set in environment variables.");
            }
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
            return hmac.ComputeHash(usernameBytes);
        }
        public static string HashPassword(string password, string username)
        {
            var pepper = Environment.GetEnvironmentVariable("WPT_PEPPER");
            if (string.IsNullOrEmpty(pepper))
            {
                LoggerService.Log("Security!", "WPT_PEPPER not set; cannot hash password.");
                throw new InvalidOperationException("Pepper is not set in environment variables.");
            }
            var salt = DeriveSaltFromUsername(username, pepper);

            using var kdf = new Rfc2898DeriveBytes(password + pepper, salt, Iterations, 
                HashAlgorithmName.SHA256);
            var hash = kdf.GetBytes(HashSize);
            return $"{Iterations}.{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string username, string password, string storedHash)
        {
            if(string.IsNullOrEmpty(storedHash)) return false;

            var pepper = Environment.GetEnvironmentVariable("WPT_PEPPER");
            if (string.IsNullOrEmpty(pepper))
            {
                LoggerService.Log("Security!", "WPT_PEPPER not set; cannot hash password.");
                throw new InvalidOperationException("Pepper is not set in environment variables.");
            }
            var parts = storedHash.Split('.');
            if (parts.Length != 2) return false;

            if (!int.TryParse(parts[0], out int iterations)) return false;

            byte[] expectedHash = Convert.FromBase64String(parts[1]);

            var salt = DeriveSaltFromUsername(username, pepper);
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
            var hashedPassword = HashPassword(password, username);
            return DbService.AddUser(username, hashedPassword, isAdmin);
        }

        public static User? Login(string username, string password)
        {
            var user = DbService.Login(username, password);
            DbService.SetAdminStatus(user.Id, true);
            return user;
        }
        //Attempts to link warframe account, but the api doesn't seem to recognize a lot of accounts, so it just sets the display name and platform.
        //Alternatively cheks the WarframeMarket API for the profile, but that is not always accurate either.
        public static async Task<bool> LinkWarframeAccount(int userId, string displayName, string platform)
        {
            return await ApiService.FetchWarframeProfile(displayName, userId);
        }
    }
}
                        