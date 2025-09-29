using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardReaderConsole.Models
{
    public class ValidationModel
    {
        public DateTime ValidatedTime { get; set; } = DateTime.Now;
        public string ValidationStatus { get; set; } = string.Empty;
        public string CardID { get; set; } = string.Empty;
    }
}
