using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VHT_Scanlog_DLL.Models
{
    public class ScanlogConfig
    {
        public List<Profile> Profiles { get; set; }
        public SmtpInfo SmtpInfo { get; set; }
        public string ChunkSize { get; set; }

        public ScanlogConfig()
        {
            Profiles = new List<Profile>();
            SmtpInfo = new SmtpInfo();
        }

    }
}
