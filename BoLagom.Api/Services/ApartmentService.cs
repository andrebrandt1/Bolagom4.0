using BoLagom.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BoLagom.Api.Services;

internal class ApartmentService
{
    private readonly IConfiguration _configuration;

    public ApartmentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IEnumerable<Apartment>> GetApartmentsAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        return await connection.QueryAsync<Apartment>(new CommandDefinition(
            """
            SELECT [Id], [ApartmentNumber], [LivingArea], [MontlyRent] AS [MonthlyRent], [PropertyId]
            FROM [Apartments]
            WHERE [PropertyId] = @PropertyId
            ORDER BY [ApartmentNumber], [Id]
            """,
            new { PropertyId = propertyId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> CreateApartmentAsync(Apartment apartment, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        apartment.ApartmentNumber = apartment.ApartmentNumber.Trim();
        apartment.Id = await connection.QuerySingleOrDefaultAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Apartments] ([ApartmentNumber], [LivingArea], [MontlyRent], [PropertyId])
            OUTPUT INSERTED.[Id]
            VALUES (@ApartmentNumber, @LivingArea, @MonthlyRent, @PropertyId)
            """,
            apartment,
            cancellationToken: cancellationToken));

        return apartment.Id > 0;
    }

    private SqlConnection CreateConnection()
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Databasanslutningen DefaultConnection saknas.");
        return new SqlConnection(connectionString);
    }
}
