using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using NLog;
using VHT_Scanlog_DLL.Models;

namespace VHT_Scanlog_DLL
{
    public class ScanlogServiceActions
    {
        private static ScanlogConfig _config;
        private static readonly object ProcessActionLock = new object();
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public ScanlogServiceActions(ScanlogConfig config)
        {
            _config = config;
        }

        public void ProcessAction(string stringFound, string fullPath)
        {
            lock (ProcessActionLock)
            {
                Thread.BeginCriticalRegion();

                Profile profile = GetCurrentProfile(stringFound, fullPath);

                if (profile.Action.SendEmail)
                {
                    SendEmail(profile.Email, _config.SmtpInfo, stringFound, Path.GetFileName(stringFound));
                }

                if (profile.Action.RunBatchFile)
                {
                    RunBatchFile(profile.Action.BatchFileName);
                }

                Thread.EndCriticalRegion();
            }
        }

        private static Profile GetCurrentProfile(string stringFound, string fullPath)
        {
            Profile profile = null;

            // Loads profiles with StringToSearch that matches stringFound
            List<Profile> profiles = _config.Profiles.Where(p => p.StringToSearch == stringFound).ToList(); 

            if (profiles.Any())
            {
                if (profiles.Count == 1)
                {
                    profile = profiles.First();
                }
                else
                {
                    // Matches profile FileName with name of file in which stringFound was discovered
                    profile = profiles.First(p => Path.GetFileName(fullPath).ToUpper().Contains(p.FileName.ToUpper())); 
                }
            }

            return profile;
        }

        private static void SendEmail(Email email, SmtpInfo smtpInfo, string stringToSearch, string fileName)
        {
            Log.Info("Composing email");
            MailMessage message = new MailMessage();

            SmtpClient smtp = new SmtpClient();
            smtp.Host = smtpInfo.SmtpServer;
            smtp.EnableSsl = smtpInfo.EnableSsl;
            smtp.Port = int.Parse(smtpInfo.SmtpPort);
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            if (smtpInfo.SmtpUserName == string.Empty || smtpInfo.SmtpPassword == string.Empty)
            {
                smtp.Credentials = CredentialCache.DefaultNetworkCredentials;
            }

            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(smtpInfo.SmtpUserName, smtpInfo.SmtpPassword);

            message.From = new MailAddress(smtpInfo.FromEmail);

            foreach (var emailAddress in email.DestinationEmails)
            {
                message.To.Add(new MailAddress(emailAddress));
            }

            message.Subject = email.EmailSubject;
            message.Body = $"The string \"{stringToSearch}\" was detected.\nserver: {Dns.GetHostName()}\nfile: {fileName}";

            try
            {
                Log.Info($"Sending email From: {message.From}, To: {message.To}, Subject: {message.Subject}");
                smtp.Send(message);
                smtp.Dispose();
                Log.Info("Email successfully sent");
            }
            catch (Exception ex)
            {
                smtp.Dispose();
                Log.Error($"Email failed to send : {ex.Message}");
            }
        }

        private static void RunBatchFile(string batchFileName)
        {
            var proc = new Process();
            proc.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory + "Batch Files\\";
            proc.StartInfo.FileName = batchFileName;
            proc.StartInfo.CreateNoWindow = true;

            try
            {
                Log.Info($"Starting batch file: {batchFileName}");
                proc.Start();
                proc.Dispose();
                Log.Info($"Started {batchFileName} successfully");
            }
            catch (Exception ex)
            {
                proc.Dispose();
                Log.Error($"Failed to start {batchFileName}: {ex.Message}");
            }
        }
    }
}
