#pragma warning disable 1591, SYSLIB0014, CA1002, CS0162
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.LOGGER;
using Jellyfin.Plugin.Newsletters.Scripts.DATA;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using Jellyfin.Plugin.Newsletters.Scripts.SCRAPER;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Newsletters.Scripts.NLDataGenerator;

public class NewsletterDataGenerator
{
    // Global Vars
    // Readonly
    private readonly PluginConfiguration config;
    private readonly string newslettersDir;
    private readonly string newsletterDataFile;

    private readonly string currRunList;
    private readonly string archiveFile;
    private readonly string myDataDir;
    private Logger logger;
    private SQLiteDatabase db;

    // Non-readonly
    // private static string append = "Append";
    // private static string write = "Overwrite";
    private List<JsonFileObj> archiveSeriesList;
    // private List<string> fileList;

    public NewsletterDataGenerator()
    {
        logger = new Logger();
        db = new SQLiteDatabase();
        config = Plugin.Instance!.Configuration;
        myDataDir = config.TempDirectory + "/Newsletters";

        archiveFile = config.MyDataDir + config.ArchiveFileName; // curlist/archive
        currRunList = config.MyDataDir + config.CurrRunListFileName;
        newsletterDataFile = config.MyDataDir + config.NewsletterDataFileName;

        archiveSeriesList = new List<JsonFileObj>();
        newslettersDir = config.NewsletterDir; // newsletterdir
        Directory.CreateDirectory(newslettersDir);
    }

    public Task GenerateDataForNextNewsletter()
    {
        try
        {
            db.CreateConnection();
            archiveSeriesList = PopulateFromArchive(db); // Files that shouldn't be processed again
            // GenerateData();
            CopyCurrRunDataToNewsletterData();
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            db.CloseConnection();
        }

        return Task.CompletedTask;
    }

    public List<JsonFileObj> PopulateFromArchive(SQLiteDatabase database)
    {
        List<JsonFileObj> myObj = new List<JsonFileObj>();

        foreach (var row in database.Query("SELECT * FROM ArchiveData;"))
        {
            logger.Debug("Inside of foreach..");
            if (row is not null)
            {
                JsonFileObj helper = new JsonFileObj();
                JsonFileObj currArcObj = helper.ConvertToObj(row);
                myObj.Add(currArcObj);
                // logger.Debug(row[0]);
            }
        }

        // ------
        // if (File.Exists(archiveFile))
        // {
        //     StreamReader sr = new StreamReader(archiveFile);
        //     string arFile = sr.ReadToEnd();
        //     foreach (string series in arFile.Split(";;;"))
        //     {
        //         JsonFileObj? currArcObj = JsonConvert.DeserializeObject<JsonFileObj?>(series);
        //         if (currArcObj is not null)
        //         {
        //             myObj.Add(currArcObj);
        //         }
        //     }

        // sr.Close();
        // }
        logger.Debug("Returning ArchObj");

        return myObj;
    }

    private void GenerateData()
    {
        StreamReader sr = new StreamReader(currRunList); // curlist/archive
        string readScrapeFile = sr.ReadToEnd();

        foreach (string? ep in readScrapeFile.Split(";;;"))
        {
            JsonFileObj? obj = JsonConvert.DeserializeObject<JsonFileObj?>(ep);
            if (obj is not null)
            {
                JsonFileObj currObj = new JsonFileObj();
                currObj.Title = obj.Title;
                archiveSeriesList.Add(currObj);
            }

            break;
        }

        sr.Close();
    }

    public string FetchImagePoster(string posterFilePath)
    {
        return UploadToImgur(posterFilePath);
    }

    private string UploadToImgur(string posterFilePath)
    {
        var wc = new WebClient();

        var values = new NameValueCollection()
        {
            { "image", Convert.ToBase64String(File.ReadAllBytes(posterFilePath)) }
        };

        wc.Headers.Add("Authorization", "Client-ID " + config.ApiKey);

        try
        {
            byte[] response = wc.UploadValues("https://api.imgur.com/3/upload.xml", values);

            string res = System.Text.Encoding.Default.GetString(response);

            logger.Debug("Imgur Response: " + res);

            logger.Info("Imgur Uploaded! Link:");
            logger.Info(res.Split("<link>")[1].Split("</link>")[0]);

            return res.Split("<link>")[1].Split("</link>")[0];
        }
        catch (WebException e)
        {
            logger.Debug("WebClient Return STATUS: " + e.Status);
            logger.Debug(e.ToString().Split(")")[0].Split("(")[1]);
            try
            {
                return e.ToString().Split(")")[0].Split("(")[1];
            }
            catch (Exception ex)
            {
                logger.Error("Error caught while trying to parse webException error: " + ex);
                return "ERR";
            }
        }
    }

    private void CopyCurrRunDataToNewsletterData()
    {
        if (File.Exists(currRunList)) // archiveFile
        {
            Stream input = File.OpenRead(currRunList);
            Stream output = new FileStream(newsletterDataFile, FileMode.Append, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            File.Delete(currRunList);
        }
    }

    // private void WriteFile(string method, string path, string value)
    // {
    //     if (method == append)
    //     {
    //         File.AppendAllText(path, value);
    //     }
    //     else if (method == write)
    //     {
    //         File.WriteAllText(path, value);
    //     }
    // }
}