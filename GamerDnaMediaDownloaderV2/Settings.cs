using System;
using System.IO;
using System.Xml.Linq;

namespace GamerDnaMediaDownloaderV2
{
	internal static class Settings
	{
		private static readonly object theGate = new object();
		private static string baseUrl = null;
		private static string pathToSave = null;
		private static int startPage = 1;
		private static bool finished = false;
		private const string path = "settings.xml";

		public static string BaseUrl
		{
			get
			{
				if (baseUrl == null)
				{
					Console.WriteLine("No base URL was specified in settings.xml, using default value!");
					baseUrl = "http://13xforever.gamerdna.com/media/";
				}
				return baseUrl;
			}
		}

		public static string PathToSave
		{
			get
			{
				if (pathToSave == null)
				{
					Console.WriteLine("No path was specified for media storage in settings.xml, using default value!");
					pathToSave = @"E:\Temp\gDNA\";
				}
				return pathToSave;
			}
		}
	
		public static int StartPage
		{
			get
			{
				lock (theGate)
					if (Finished) startPage = 1;
				return startPage;
			}
			set
			{
				lock (theGate)
					startPage = value;
				Save();
			}
		}

		public static bool Finished
		{
			get { return finished; }
			set
			{
				finished = value;
				Save();
			}
		}

		public static void Load()
		{
			if (!File.Exists(path)) return;

			lock(theGate)
			{
				XDocument doc;
				using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
					doc = XDocument.Load(stream);
				var root = doc.Root;
				baseUrl = root.Element("baseUrl").Return(e => e.Value);
				pathToSave = root.Element("pathToSave").Return(e => e.Value);
				startPage = (int)root.Attribute("startPage");
				finished = (bool)root.Attribute("finished");
				Save();
			}
		}

		public static void Save()
		{
			var doc = new XDocument(new XElement("settings",
												new XAttribute("startPage", StartPage),
												new XAttribute("finished", Finished),
												new XElement("baseUrl", new XText(BaseUrl)),
												new XElement("pathToSave", new XText(PathToSave))
										));
			lock (theGate)
				using (var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
					doc.Save(stream);
		}
	}
}