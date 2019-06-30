using System;

namespace ConnectionDialog.Library.Extensions
{
	public static class ExceptionExtensions
	{
		public static string FullMessage(this Exception exception)
		{
			string result = exception.Message;

			Exception inner = exception.InnerException;
			while (inner != null)
			{
				result += $"\r\n- {inner.Message}";
				inner = inner.InnerException;
			}

			return result;
		}
	}
}