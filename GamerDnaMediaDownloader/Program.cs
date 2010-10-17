using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GamerDnaMediaDownloader
{
	class Program
	{
		static void Main(string[] args)
		{
			const string pathToSave = @"E:\Temp\gDNA\";

			var pagesToProcess = MediaPageGetter.GetMediaPages(1);
			Log.Info("Press any key to exit...");
			Console.ReadKey();
		}
	}
}
