using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ValidationService.Models
{
  public class Validation
  {
    public DateTime ValidatedTime { get; set; } = DateTime.Now;
    public string ValidationStatus { get; set; } = string.Empty;
    [JsonPropertyName("CardUID")]
    public string CardID { get; set; } = string.Empty;
  }
}
