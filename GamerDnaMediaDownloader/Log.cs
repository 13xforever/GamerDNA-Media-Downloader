using System;

namespace GamerDnaMediaDownloader
{
	internal static class Log
	{
		private static readonly object Locker = new object();

		public static void Error(string message, params object[] args)
		{
			WriteColored(ConsoleColor.Red, message, args);
		}

		public static void Warning(string message, params object[] args)
		{
			WriteColored(ConsoleColor.Yellow, message, args);
		}

		public static void Info(string message, params object[] args)
		{
			WriteColored(ConsoleColor.White, message, args);
		}

		public static void Debug(string message, params object[] args)
		{
			WriteColored(ConsoleColor.Cyan, message, args);
		}

		private static void WriteColored(ConsoleColor color, string message, params object[] args)
		{
			lock (Locker)
			{
				Console.ForegroundColor = color;
				Console.WriteLine(message, args);
				Console.ResetColor();
			}
		}
	}
}