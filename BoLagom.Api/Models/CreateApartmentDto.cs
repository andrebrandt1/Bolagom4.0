namespace BoLagom.Api.Models;

public class CreateApartmentDto
{
    public string? ApartmentNumber { get; set; }
    public decimal? LivingArea { get; set; }
    public int? MonthlyRent { get; set; }
}
