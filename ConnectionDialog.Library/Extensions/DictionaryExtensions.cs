using System.Collections.Generic;
using System.Linq;

namespace ConnectionDialog.Library.Static
{
	public static class DictionaryExtensions
	{
		public static string CoalesceValues(this Dictionary<string, string> dictionary, params string[] keys)
		{
			string firstKey = keys.First(key => dictionary.ContainsKey(key));
			return dictionary[firstKey];
		}
	}
}