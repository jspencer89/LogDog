using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VHT_Scanlog_DLL.Models
{
    public class Action
    {
        public bool SendEmail { get; set; }
        public bool RunBatchFile { get; set; }

        public string BatchFileName { get; set; }
    }
}
