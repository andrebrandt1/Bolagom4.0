namespace BoLagom.Api.Models;

internal class Apartment
{
    public int Id { get; set; }
    public required string ApartmentNumber { get; set; }
    public decimal LivingArea { get; set; }
    public int MonthlyRent { get; set; }
    public int PropertyId { get; set; }
}
