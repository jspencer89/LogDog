using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VHT_Scanlog_DLL.Models
{
    public class SmtpInfo
    {
        public string SmtpServer { get; set; }
        public string SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
        public string SmtpUserName { get; set; }
        public string SmtpPassword { get; set; }
        public string FromEmail { get; set; }
    }
}
