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
        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
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
        public static void LinkWarframeAccount(int userId, string displayName, string platform)
        {
            DbService.SetUserWfAccount(userId, displayName, platform);
        }
    }
}
