using JsonSettings;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ConnectionDialog.Library.Models
{
    public class ConnectionDialogSettings : JsonSettingsBase
    {
        public override string CompanyName => "Adam O'Neil";
        public override string ProductName => "Connection Strings";
        public override Scope Scope => Scope.User;
        public override string Filename => "settings.json";

        public List<Server> Servers { get; set; } = new List<Server>();

        public class Server
        {
            [JsonProtect(DataProtectionScope.CurrentUser)]
            public string Name { get; set; }

            public List<User> Users { get; set; } = new List<User>();

            internal void AddUser(string userName, string password)
            {
                if (Users == null) Users = new List<User>();
                User result = Users.SingleOrDefault(u => u.Name.ToLower().Equals(userName.ToLower()));
                if (result == null)
                {
                    result = new User() { Name = userName, Password = password };
                    Users.Add(result);
                }
                else
                {
                    result.Password = password;
                }
            }
        }

        public class User
        {
            [JsonProtect(DataProtectionScope.CurrentUser)]
            public string Name { get; set; }

            [JsonProtect(DataProtectionScope.CurrentUser)]
            public string Password { get; set; }
        }

        internal Server AddServer(string name)
        {
            if (Servers == null) Servers = new List<Server>();
            Server result = Servers.SingleOrDefault(s => s.Name.ToLower().Equals(name.ToLower()));
            if (result != null) return result;

            result = new Server() { Name = name };
            Servers.Add(result);
            return result;
        }
    }
}