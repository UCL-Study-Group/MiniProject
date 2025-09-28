namespace Registration_Service.Models;

public class Registration
{
    public required string Name { get; set; }
    public required string Serial { get; set; }
    
    public byte[] GetParsedSerial => Convert.FromHexString(Serial);
}