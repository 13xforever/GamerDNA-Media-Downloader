using System.IO;
using System.Xml.Linq;

namespace GamerDnaMediaDownloaderV2
{
	internal static class Settings
	{
		private static readonly object theGate = new object();
		private static int startPage = 1;
		private static bool finished = false;
		private const string path = "settings.xml";

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
				startPage = (int)doc.Root.Attribute("startPage");
				Finished = (bool)doc.Root.Attribute("finished");
			}
		}

		public static void Save()
		{
			var root = new XElement("settings");
			root.Add(new XAttribute("startPage", startPage));
			root.Add(new XAttribute("finished", finished));
			var doc = new XDocument();
			doc.Add(root);

			lock (theGate)
				using (var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
					doc.Save(stream);
		}
	}
}