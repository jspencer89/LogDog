using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace VHT_Scanlog_Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
#if DEBUG
            var scanlogService = new VHT_Scanlog_Service();
            scanlogService.OnDebug();
            Thread.Sleep(Timeout.Infinite);
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new VHT_Scanlog_Service()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
