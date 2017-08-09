using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VHT_Scanlog_DLL.Models
{
    public class Email
    {
        public string EmailSubject { get; set; }
        public List<string> DestinationEmails { get; set; }

        public Email()
        {
            DestinationEmails = new List<string>();
        }
    }
}
