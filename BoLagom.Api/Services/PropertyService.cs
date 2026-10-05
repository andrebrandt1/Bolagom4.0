using BoLagom.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BoLagom.Api.Services;

internal class PropertyService
{
    private readonly IConfiguration _configuration;

    public PropertyService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IEnumerable<Property>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        return await connection.QueryAsync<Property>(new CommandDefinition(
            """
            SELECT
                [Id],
                [Name],
                [Address],
                [Floors],
                [PortCode]
            FROM [Properties]
            """,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> PropertyExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        return await connection.QuerySingleAsync<bool>(new CommandDefinition(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM [Properties] WHERE [Id] = @Id) THEN 1 ELSE 0 END AS bit)",
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> CreatePropertyAsync(Property property, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        property.Name = property.Name.Trim();
        property.Address = property.Address.Trim();
        property.Floors = property.Floors.Trim();
        property.PortCode = property.PortCode.Trim();

        property.Id = await connection.QuerySingleOrDefaultAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Properties] ([Name], [Address], [Floors], [PortCode])
            OUTPUT INSERTED.[Id]
            VALUES (@Name, @Address, @Floors, @PortCode)
            """,
            property,
            cancellationToken: cancellationToken));

        return property.Id > 0;
    }

    public async Task<bool> DeletePropertyAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        return (await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [Properties] WHERE [Id] = @Id",
            new { Id = id },
            cancellationToken: cancellationToken))) > 0;
    }

    public async Task<bool> UpdatePropertyAsync(Property property, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        return (await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Properties]
            SET [Name] = @Name,
                [Address] = @Address,
                [Floors] = @Floors,
                [PortCode] = @PortCode
            WHERE [Id] = @Id
            """,
            new
            {
                Id = property.Id,
                Name = property.Name.Trim(),
                Address = property.Address.Trim(),
                Floors = property.Floors.Trim(),
                PortCode = property.PortCode.Trim()
            },
            cancellationToken: cancellationToken))) > 0;
    }

    private SqlConnection CreateConnection()
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Databasanslutningen DefaultConnection saknas.");

        return new SqlConnection(connectionString);
    }
}
