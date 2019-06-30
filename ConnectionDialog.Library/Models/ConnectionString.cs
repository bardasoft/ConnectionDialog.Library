using JsonSettings;
using System.Security.Cryptography;

namespace ConnectionDialog.Library.Models
{
	public class ConnectionString
	{
		[JsonProtect(DataProtectionScope.CurrentUser)]
		public string Value { get; set; }
	}
}