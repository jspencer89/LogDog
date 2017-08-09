using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NLog;
using VHT_Scanlog_DLL.Models;

namespace VHT_Scanlog_DLL
{
    public class ScanlogServiceConfiguration
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string ScanlogConfigFileName = AppDomain.CurrentDomain.BaseDirectory + "ScanlogConfig.xml";

        //private static void SerializeConfig(ScanlogConfig c)
        //{
        //    var p = new Profile();
        //    p.FileName = "Log";
        //    p.LogDirectory = "C:\\VHLogs";
        //    p.StringToSearch = "All your VHT are broken!";
        //    p.Action.SendEmail = true;
        //    p.Action.RunBatchFile = true;
        //    p.Action.BatchFileName = "Hello.bat";
        //    p.Email.EmailSubject = "Scanlog Found A Thing";
        //    p.Email.DestinationEmails.Add("jspencer@virtualhold.com");
        //    c.Profiles.Add(p);

        //    c.SmtpInfo.SmtpServer = "smtp.gmail.com";
        //    c.SmtpInfo.SmtpPort = "587";
        //    c.SmtpInfo.EnableSsl = true;
        //    c.SmtpInfo.SmtpUserName = "slaughter.melon132@gmail.com";
        //    c.SmtpInfo.SmtpPassword = "P1k@chu!";
        //    c.SmtpInfo.FromEmail = "slaighter.melon132@gmail.com";

        //    c.MainLogFilePath = "Poop";
        //    c.MaxLogSizeK = "1024";
        //    c.ChunkSize = "1024";

        //    try
        //    {
        //        System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(c.GetType());
        //        x.Serialize(Console.Out, c);
        //        Console.WriteLine();
        //        Console.ReadLine();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        Console.ReadLine();
        //    }
        //}

        public static ScanlogConfig ReadConfig()
        {
            Log.Info($"Reading ScanlogConfig.xml from {ScanlogConfigFileName}");

            ScanlogConfig config;

            XmlSerializer serializer = new XmlSerializer(typeof(ScanlogConfig));
            serializer.UnknownAttribute += SerializerUnknownAttribute;
            serializer.UnknownElement += SerializerUnknownElement;

            try
            {
                using (Stream stream = new FileStream(ScanlogConfigFileName, FileMode.Open))
                {
                    config = (ScanlogConfig)serializer.Deserialize(stream);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return null;
            }

            return config;
        }

        private static void SerializerUnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            Log.Error($"Unknown XML attribute: \"{e.Attr.Name}\" line: {e.LineNumber} position: {e.LinePosition}");
        }

        private static void SerializerUnknownElement(object sender, XmlElementEventArgs e)
        {
            Log.Error($"Unknown XML element: \"{e.Element.Name}\" line: {e.LineNumber} position: {e.LinePosition}");
        }

        public static bool IsConfigValid(ScanlogConfig config)
        {
            Log.Info("Validating loaded Scanlog configuration");

            bool isConfigValid = true;

            if (config == null)
            {
                Log.Error("Scanlog Configuration is null");
                return false;
            }
            if (config.Profiles == null || !config.Profiles.Any())
            {
                Log.Error("No Profiles Configured");
                isConfigValid = false;
            }
            else
            {
                foreach (var p in config.Profiles)
                {
                    if (string.IsNullOrEmpty(p.FileName))
                    {
                        Log.Error("One or more configured profiles are missing a FileName");
                        isConfigValid = false;
                        break;
                    }
                    if (string.IsNullOrEmpty(p.LogDirectory))
                    {
                        Log.Error($"Profile for {p.FileName} is missing a LogDirectory");
                        isConfigValid = false;
                        break;
                    }
                    if (string.IsNullOrEmpty(p.StringToSearch))
                    {
                        Log.Error($"Profile for {p.FileName} is missing a StringToSearch");
                        isConfigValid = false;
                        break;
                    }
                    if (!p.Action.SendEmail && !p.Action.RunBatchFile)
                    {
                        Log.Error($"Profile for {p.FileName} does not have an Action configured");
                        isConfigValid = false;
                        break;
                    }
                    if (p.Action.SendEmail && string.IsNullOrEmpty(p.Email.EmailSubject))
                    {
                        Log.Error($"Profile for {p.FileName} is configured to SendEmail with no Emailsubject");
                        isConfigValid = false;
                        break;
                    }
                    if (p.Action.SendEmail &&
                        (p.Email.DestinationEmails == null || !p.Email.DestinationEmails.Any()))
                    {
                        Log.Error($"Profile for {p.FileName} is configured to SendEmail with no DestinationEmails");
                        isConfigValid = false;
                        break;
                    }
                    if (p.Action.SendEmail && string.IsNullOrEmpty(config.SmtpInfo.SmtpServer))
                    {
                        Log.Error($"Profile for {p.FileName} is configured to SendEmail with no defined SmtpServer");
                        isConfigValid = false;
                        break;
                    }
                    if (p.Action.SendEmail && string.IsNullOrEmpty(config.SmtpInfo.FromEmail))
                    {
                        Log.Error($"Profile for {p.FileName} is configured to SendEmail with no FromEmail");
                        isConfigValid = false;
                        break;
                    }
                    if (p.Action.RunBatchFile && string.IsNullOrEmpty(p.Action.BatchFileName))
                    {
                        Log.Error(
                            $"Profile for {p.FileName} is configured to RunBatchFile with no BatchFileName configured");
                        isConfigValid = false;
                        break;
                    }
                }
            }

            int i;
            if (string.IsNullOrEmpty(config.SmtpInfo.SmtpPort) || !int.TryParse(config.SmtpInfo.SmtpPort, out i))
            {
                config.SmtpInfo.SmtpPort = "25";
                Log.Warn("No SmtpPort configured. Setting SmtpPort to default: 25");
            }
            if (string.IsNullOrEmpty(config.ChunkSize))
            {
                config.ChunkSize = "4096";
                Log.Warn("No ChunkSize configured. Setting ChunkSize to default: 4096 bytes");
            }

            if (isConfigValid)
            {
                LogCurrentConfig(config);
            }

            return isConfigValid;
        }

        private static void LogCurrentConfig(ScanlogConfig config)
        {
            Log.Info("The following configuration has been validated...");
            Log.Info("Begin configuration");
            for (int index = 0; index < config.Profiles.Count; index++)
            {
                var p = config.Profiles[index];
                Log.Info($"\tProfile {index + 1}");
                Log.Info($"\t\tFileName: {p.FileName}");
                Log.Info($"\t\tLogDirectory: {p.LogDirectory}");
                Log.Info($"\t\tStringToSearch: {p.StringToSearch}");

                Log.Info($"\t\tSendEmail: {p.Action.SendEmail}");
                Log.Info($"\t\tRunBatchFile: {p.Action.RunBatchFile}");
                Log.Info($"\t\tBatchFileName: {p.Action.BatchFileName}");

                Log.Info($"\t\tEmailSubject: {p.Email.EmailSubject}");

                foreach (var e in p.Email.DestinationEmails)
                {
                    Log.Info($"\t\tDestinationEmail: {e}");
                }
            }

            Log.Info($"\tSmtpServer: {config.SmtpInfo.SmtpServer}");
            Log.Info($"\tSmtpPort: {config.SmtpInfo.SmtpPort}");
            Log.Info($"\tEnableSsl: {config.SmtpInfo.EnableSsl}");
            Log.Info($"\tSmtpUserName: {config.SmtpInfo.SmtpUserName}");
            Log.Info($"\tSmtpPassword: {config.SmtpInfo.SmtpPassword}");
            Log.Info($"\tFromEmail: {config.SmtpInfo.FromEmail}");

            Log.Info($"\tChunkSize: {config.ChunkSize}");
        }

    }
}
