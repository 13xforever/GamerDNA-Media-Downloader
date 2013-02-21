using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using Raven.Client;

namespace GamerDnaMediaDownloaderV2
{
	internal static class MediaPageGetter
	{
		internal static IEnumerable<Uri> GetMediaPages(IDocumentStore store)
		{
			int startPage = Settings.StartPage;
			string baseUrl = Settings.BaseUrl;
			string mediaListUrl = baseUrl;
			if (startPage > 1) mediaListUrl += string.Format("?page={0}", startPage);
			var searchResultElement = new XElement("div");
			bool allLinksAreOld = false;
			while (!string.IsNullOrEmpty(mediaListUrl) && !(allLinksAreOld && Settings.Finished))
			{
				allLinksAreOld = true;
				Log.Info("Processing page {0} of media list", startPage);
				string pageContent = GetPageContent(new Uri(mediaListUrl));
				const string divListContainer = "<div class=\"corners-center\">";
				int searchResultStart = pageContent.IndexOf(divListContainer);
				searchResultStart = pageContent.IndexOf(divListContainer, searchResultStart + 1);
				if (searchResultStart == -1)
					Thread.Sleep(1000);
				else
				{
					int searchResultEnd = pageContent.IndexOf("<div class=\"corners-bottom\"", searchResultStart + 1);
					if (searchResultEnd == -1)
						Log.Warning("Can't find the end of xml subtree.");
					else
					{
						var mediaInfos = Enumerable.Empty<XElement>();
						try
						{
							searchResultElement = XElement.Parse(Sanitize(pageContent.Substring(searchResultStart, searchResultEnd - searchResultStart)));
							mediaInfos = searchResultElement.XPathSelectElements("/div[@class='media_list']/div[@class='list_body']/div[@class='media_item_container']/div[@class='media_item']/div[@class='media_wrapper']/a");
						}
						catch (Exception e)
						{
							Log.Error("Error while processing page {0} of media list: {1}", startPage, e.Message);
							continue;
						}
						foreach (var mediaInfo in mediaInfos)
						{
							var result = mediaInfo.Attribute("href").Return(attr => new Uri(attr.Value));
							using (var session = store.OpenSession())
								allLinksAreOld &= session.Query<Media>().Any(media => media.PageUrl == result);
							yield return result;
						}
					}
					XElement nextPageLink = searchResultElement.XPathSelectElement("//a[@class='next_page']");
					if (nextPageLink == null)
					{
						mediaListUrl = null;
						Settings.Finished = true;
						Log.Info("Procession of Media List page is finished");
					}
					else
					{
						mediaListUrl = string.Format("{0}?page={1}", baseUrl, ++startPage);
						Settings.StartPage = startPage;
					}
				}
			}
		}

		internal static void GetImageInfo(Media media)
		{
			string pageContent = GetPageContent(media.PageUrl);
			int searchResultStart = pageContent.IndexOf("<div id=\"bd_shell\">");
			if (searchResultStart == -1)
			{
				if (!string.IsNullOrEmpty(pageContent)) Log.Error("Can't find content for page {0}", media.PageUrl);
				return;
			}

			int searchResultEnd = pageContent.IndexOf("<!-- End Container -->", searchResultStart + 1);
			if (searchResultEnd == -1)
			{
				Log.Warning("Can't find the end of xml subtree.");
				return;
			}
			
			try
			{
				var resultElement = XElement.Parse(Sanitize(pageContent.Substring(searchResultStart, searchResultEnd - searchResultStart)));
				var title = resultElement.XPathSelectElement("/div[@class='two_col_shell content_hd']/div[@class='col_primary']/div[@class='source']/div[@class='info']/h1");
				var description = resultElement.XPathSelectElement("/div[@class='two_col_shell content_hd']/div[@class='col_primary']/p");
				var mediaUrl = resultElement.XPathSelectElement("/div[@class='content_bd']/a");
				media.MediaUrl = mediaUrl.Return(url => url.Attribute("href")).Return(attr => attr.Value).Unless(string.IsNullOrWhiteSpace).Return(s=>new Uri(s));
				media.Title = title.Value.Trim();
				media.Description = description
					.Return(e =>
					        	{
					        		var r = e.CreateReader();
					        		r.MoveToContent();
					        		return r.ReadInnerXml();
					        	})
					.Return(value => value.Trim())
					.If(value => !string.IsNullOrEmpty(value));
				media.Processed = true;
				Log.Info("Media '{0}' was successfully processed", media.Title);
			}
			catch (Exception e)
			{
				Log.Error("Error while processing page {0}: {1}", media.PageUrl, e.Message);
			}
		}

		internal static void SaveMedia(Media media)
		{
			string pathToSave = Settings.PathToSave;

			string localFilename = media.MediaUrl.Return(uri => uri.LocalPath).Return(Path.GetFileName).Return(filename => Path.Combine(pathToSave, filename));
			if (string.IsNullOrEmpty(localFilename))
			{
				Log.Error("Invalid filename for url {0}", media.MediaUrl);
				return;
			}
			if (File.Exists(localFilename))
			{
				Log.Info("File already exists. Skipping.");
				media.LocalPath = localFilename;
				media.Saved = true;
				return;
			}

			byte[] content = GetData(media.MediaUrl);
			if (content == null)
			{
				//Log.Error("Can't get content for {0}", media.MediaUrl);
				return;
			}


			try
			{
				File.WriteAllBytes(localFilename, content);
				media.LocalPath = localFilename;
				media.Saved = true;
				Log.InfoGreen("Media '{0}' was saved on disk", media.Title);
			}
			catch(Exception e)
			{
				Log.Error("Can't save media from {0}: {1}", media.MediaUrl, e.Message);
			}
		}

		private static string GetPageContent(Uri url)
		{
			string pageContent = "";
			WebRequest request = WebRequest.Create(url);
			WebResponse response = null;
			try
			{
				request.Timeout = 60*1000;
				response = request.GetResponse();
			}
			catch (Exception e)
			{
				//Log.Error("Can't get content for {0}: {1}", url, e.Message);
			}

			if (response != null)
				using (Stream stream = response.GetResponseStream())
					if (stream != null)
						using (var reader = new StreamReader(stream, Encoding.UTF8))
							pageContent = reader.ReadToEnd();
			return pageContent;
		}

		private static byte[] GetData(Uri url)
		{
			byte[] content = null;
			WebRequest request = WebRequest.Create(url);
			WebResponse response = null;
			try
			{
				request.Timeout = 5*60*1000;
				response = request.GetResponse();
			}
			catch (Exception e)
			{
				//Log.Error("Can't get content for {0}: {1}", url, e.Message);
			}
			if (response != null)
				using (Stream stream = response.GetResponseStream())
					if (stream != null)
						using (var memoryStream = new MemoryStream())
						{
							stream.CopyTo(memoryStream);
							content = memoryStream.ToArray();
						}
			return content;
		}

		private static string Sanitize(string content)
		{
			var sb = new StringBuilder(content);
			sb.Replace("<br>", "<br />");

			sb.Replace("Ballzzz >_<", "Ballzzz &gt;_&lt;");

			sb.Replace("&network", "&amp;network");
			sb.Replace("&url", "&amp;url");
			sb.Replace("&title", "&amp;title");
			sb.Replace("& ", "&amp; ");
			sb.Replace("&2.", "&amp;2. ");

			sb.Replace("&laquo;", "&#xbb;");
			sb.Replace("&raquo;", "&#xab;");
			sb.Replace("&hellip;", "&#x2026;");
			return sb.ToString();
		}
	}
}
