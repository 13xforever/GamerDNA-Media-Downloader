using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;

namespace GamerDnaMediaDownloader
{
	internal static class MediaPageGetter
	{
		internal static IEnumerable<Uri> GetMediaPages()
		{
			int startPage = CacheBag.GetStartingPage();
			const string baseUrl = "http://13xforever.gamerdna.com/media/";
			string mediaListUrl = baseUrl;
			if (startPage > 1) mediaListUrl += string.Format("?page={0}", startPage);
			var searchResultElement = new XElement("div");
			bool allLinksAreOld = false;
			while (!string.IsNullOrEmpty(mediaListUrl) && !(allLinksAreOld && CacheBag.IsProcessionFinished()))
			{
				allLinksAreOld = true;
				Log.Info("Processing page {0} of media list", startPage);
				string pageContent = GetPageContent(mediaListUrl);
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
							Log.Error("Error while processing page {0} of user list: {1}", startPage, e.Message);
						}
						foreach (var mediaInfo in mediaInfos)
						{
							var result = mediaInfo.Attribute("href").Return(attr => new Uri(attr.Value));
							allLinksAreOld &= CacheBag.ContainsMediaPage(result);
							yield return result;
						}
					}
					XElement nextPageLink = searchResultElement.XPathSelectElement("//a[@class='next_page']");
					if (nextPageLink == null)
					{
						mediaListUrl = null;
						CacheBag.MarkProcessionStatusAsFinished();
						Log.Info("Procession of Media List page is finished");
					}
					else
					{
						mediaListUrl = string.Format("{0}?page={1}", baseUrl, ++startPage);
						CacheBag.UpdateProcessionStatus(startPage);
					}
				}
			}
		}

		internal static void GetImageInfo(MediaInfo mediaInfo)
		{
			string pageContent = GetPageContent(mediaInfo.MediaPageUrl);
			int searchResultStart = pageContent.IndexOf("<div id=\"bd_shell\">");
			if (searchResultStart == -1)
			{
				if (!string.IsNullOrEmpty(pageContent)) Log.Error("Can't find content for page {0}", mediaInfo.MediaPageUrl);
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
				mediaInfo.MediaUrl = mediaUrl.Return(url => url.Attribute("href")).Return(attr => attr.Value);
				mediaInfo.Title = title.Value.Trim();
				mediaInfo.Description = description
					.Return(e =>
					        	{
					        		var r = e.CreateReader();
					        		r.MoveToContent();
					        		return r.ReadInnerXml();
					        	})
					.Return(value => value.Trim())
					.If(value => !string.IsNullOrEmpty(value));
				mediaInfo.Processed = true;
				CacheBag.Flush();
				Log.Info("Media '{0}' was successfully processed", mediaInfo.Title);
			}
			catch (Exception e)
			{
				Log.Error("Error while processing page {0}: {1}", mediaInfo.MediaPageUrl, e.Message);
			}
		}

		internal static void SaveMedia(MediaInfo mediaInfo)
		{
			const string pathToSave = @"E:\Temp\gDNA\";

			byte[] content = GetData(mediaInfo.MediaUrl);
			if (content == null)
			{
				//Log.Error("Can't get content for {0}", mediaInfo.MediaUrl);
				return;
			}

			string localFilename = new Uri(mediaInfo.MediaUrl).Return(uri => uri.LocalPath).Return(Path.GetFileName).Return(filename => Path.Combine(pathToSave, filename));
			if (string.IsNullOrEmpty(localFilename))
			{
				Log.Error("Invalid filename for url {0}", mediaInfo.MediaUrl);
				return;
			}

			try
			{
				File.WriteAllBytes(localFilename, content);
				mediaInfo.LocalFilename = localFilename;
				mediaInfo.Saved = true;
			}
			catch(Exception e)
			{
				Log.Error("Can't save media from {0}: {1}", mediaInfo.MediaUrl, e.Message);
			}
		}

		private static string GetPageContent(string url)
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

		private static byte[] GetData(string url)
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

			sb.Replace("&network", "&amp;network");
			sb.Replace("&url", "&amp;url");
			sb.Replace("&title", "&amp;title");
			sb.Replace("& ", "&amp; ");

			sb.Replace("&laquo;", "&#xbb;");
			sb.Replace("&raquo;", "&#xab;");
			sb.Replace("&hellip;", "&#x2026;");
			return sb.ToString();
		}
	}
}
