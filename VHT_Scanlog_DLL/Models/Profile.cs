using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VHT_Scanlog_DLL.Models
{
    public class Profile
    {
        public string FileName { get; set; }
        public string LogDirectory { get; set; }
        public string StringToSearch { get; set; }

        public Action Action { get; set; }
        public Email Email { get; set; }

        public Profile()
        {
            Email = new Email();
            Action = new Action();
        }
    }
}
