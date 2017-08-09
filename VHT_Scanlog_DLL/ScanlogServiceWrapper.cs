using System;
using NLog;

namespace VHT_Scanlog_DLL
{
    public class ScanlogServiceWrapper
    {
        public ScanlogServiceController Controller { get; set; }
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public ScanlogServiceWrapper()
        {
            Controller = new ScanlogServiceController();
        }

        public bool Start()
        {
            var started = false;
            try
            {
                started = Controller.Start();

                if (started)
                {
                    Log.Info("Scanlog Service Successfully started");
                }
                else
                {
                    Log.Error("Encountered an issue during startup, stopping service...");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }

            return started;
        }

        public void Stop()
        {
            try
            {
                Controller.Stop();
                Log.Info("Scanlog Service stopped");
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        //public static string GetDllVersion()
        //{
        //    return typeof(ScanlogServiceWrapper).Assembly.GetName().Version.ToString();
        //}
    }
}
