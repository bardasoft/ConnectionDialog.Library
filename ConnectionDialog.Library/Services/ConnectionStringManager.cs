using ConnectionDialog.Library.Models;
using ConnectionDialog.Library.Static;
using JsonSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConnectionDialog.Library.Services
{
	public class ConnectionStringManager
	{
        private readonly string _companyName;
        private readonly string _productName;

        public ConnectionStringManager(string companyName, string productName)
        {
            _companyName = companyName;
            _productName = productName;
        }

		public static Dictionary<string, string> Parse(string connectionString)
		{
			var items = connectionString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			var keyPairs = items.Where(s => HasTwoParts(s)).Select(s =>
			{
				var parts = s.Split('=');
				return new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim());
			});
			return keyPairs.ToDictionary(item => item.Key, item => item.Value);
		}

		private static bool HasTwoParts(string input)
		{
			var parts = input.Split('=');
			return (parts.Length == 2);
		}

		public static bool HasPassword(string connectionString)
		{
			var dictionary = Parse(connectionString);
			return dictionary.ContainsKey("Password");
		}

		/// <summary>
		/// If a connection string contains a password, then a reference to DPAPI-encrypted version
		/// of it is returned as a file reference. Otherwise the raw connection string is returned
		/// </summary>
		public string EncryptIfPasswordPresent(string rawConnectionString)
		{
			var parts = Parse(rawConnectionString);
			return (parts.ContainsKey("Password")) ? GetConnectionReference(rawConnectionString) : rawConnectionString;
		}

		public string Decrypt(string rawConnectionString)
		{
			if (rawConnectionString.StartsWith("@"))
			{
				string path = GetSecureConnectionPath(rawConnectionString.Substring(1));
				if (File.Exists(path))
				{
					var connection = JsonFile.Load<ConnectionString>(path);
					return connection.Value;
				}
				else
				{
					throw new FileNotFoundException($"File not found: {path}");
				}
			}
			else
			{
				return rawConnectionString;
			}
		}

		private string GetConnectionReference(string connectionString)
		{
			var parts = Parse(connectionString);
			string fileName = $"{parts.CoalesceValues("Data Source", "Server")}-{parts["User Id"]}-{parts.CoalesceValues("Database", "Catalog")}.json";
			string path = GetSecureConnectionPath(fileName);
			JsonFile.Save(path, new ConnectionString() { Value = connectionString });
			return $"@{fileName}";
		}

		private string GetSecureConnectionPath(string fileName)
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), _companyName, _productName, fileName);
		}
	}
}