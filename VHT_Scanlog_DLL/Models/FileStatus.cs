using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VHT_Scanlog_DLL.Models
{
    public class FileStatus
    {
        public bool IsActive { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime LastRead { get; set; }
        public long ReadFileFromHere { get; set; }
        public List<string> StringsFound { get; set; }

        public FileStatus()
        {
            LastRead = DateTime.MinValue;
            StringsFound = new List<string>();
        }
    }
}
