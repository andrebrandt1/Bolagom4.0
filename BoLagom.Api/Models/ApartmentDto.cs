namespace BoLagom.Api.Models;

public class ApartmentDto
{
    public int Id { get; set; }
    public required string ApartmentNumber { get; set; }
    public decimal LivingArea { get; set; }
    public int MonthlyRent { get; set; }
    public int PropertyId { get; set; }
}
