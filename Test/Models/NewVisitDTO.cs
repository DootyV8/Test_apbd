namespace Test.Models;

public class NewVisitDTO
{
    public int visitId { get; set; }
    public int clientId { get; set; }
    public string mechanicLicenceNumber { get; set; }
    public List<VisitServiceInputDTO> services { get; set; }
}