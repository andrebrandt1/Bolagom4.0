namespace BoLagom.Api.Models;

class Property
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string Floors { get; set; }
    public required string PortCode { get; set; }
}
