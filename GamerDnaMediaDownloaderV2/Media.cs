using System;

namespace GamerDnaMediaDownloaderV2
{
	public class Media
	{
		public int Id;
		public Uri PageUrl;
		public Uri MediaUrl;
		public string Title;
		public string Description;
		public string LocalPath;
		public bool Processed;
		public bool Saved;
	}
}