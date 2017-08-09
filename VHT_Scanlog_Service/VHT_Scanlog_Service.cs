using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using VHT_Scanlog_DLL;

namespace VHT_Scanlog_Service
{
    public partial class VHT_Scanlog_Service : ServiceBase
    {
        private ScanlogServiceWrapper ScanlogWrapper;
        public VHT_Scanlog_Service()
        {
            InitializeComponent();
        }

        public void OnDebug()
        {
            OnStart(null);
        }
        protected override void OnStart(string[] args)
        {
            ScanlogWrapper = new ScanlogServiceWrapper();
            if (!ScanlogWrapper.Start())
            {
                OnStop();
            }
        }

        protected override void OnStop()
        {
            ScanlogWrapper.Stop();
        }
    }
}
