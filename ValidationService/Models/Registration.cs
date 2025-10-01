using System.Text.Json.Serialization;

namespace Registration_Service.Models;

public class Registration
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("serial")]
    public required string Serial { get; set; }
    
    public byte[] GetParsedSerial => Convert.FromHexString(Serial);
}