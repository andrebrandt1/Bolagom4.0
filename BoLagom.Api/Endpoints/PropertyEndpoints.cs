using BoLagom.Api.Models;
using BoLagom.Api.Services;
using Microsoft.Data.SqlClient;

namespace BoLagom.Api.Endpoints;

public static class PropertyEndpoints
{
    public static WebApplication AddPropertyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/properties", static async (PropertyService propertyService, CancellationToken cancellationToken) =>
        {
            IEnumerable<Property> properties = await propertyService.GetPropertiesAsync(cancellationToken);
            return properties.Select(property => new PropertyDto
            {
                Id = property.Id,
                Name = property.Name,
                Address = property.Address,
                Floors = property.Floors
            });
        });



        app.MapPost("/api/properties", static async (CreatePropertyDto request, PropertyService propertyService, CancellationToken cancellationToken) =>
        {
            var errors = ValidateProperty(request.Name, request.Address, request.Floors, request.PortCode);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var property = new Property
            {
                Name = request.Name,
                Address = request.Address,
                Floors = request.Floors,
                PortCode = request.PortCode
            };

            bool created = await propertyService.CreatePropertyAsync(property, cancellationToken);
            if (!created)
            {
                return Results.Problem("Fastigheten kunde inte skapas.");
            }

            return Results.Created("/api/properties", new PropertyDto
            {
                Id = property.Id,
                Name = property.Name,
                Address = property.Address,
                Floors = property.Floors
            });
        });

        app.MapDelete("/api/properties/{id:int}", static async (int id, PropertyService propertyService, CancellationToken cancellationToken) =>
        {
            if (id <= 0)
            {
                return Results.BadRequest(new { Message = "ID måste vara ett positivt heltal." });
            }

            try
            {
                bool deleted = await propertyService.DeletePropertyAsync(id, cancellationToken);
                if (!deleted)
                {
                    return Results.NotFound(new { Message = "Fastigheten hittades inte." });
                }

                return Results.NoContent();
            }
            catch (SqlException exception) when (exception.IsApartmentPropertyForeignKeyViolation())
            {
                return Results.Conflict(new { Message = "Fastigheten kan inte raderas eftersom den har lägenheter." });
            }
        });

        app.MapPut("/api/properties/{id:int}", static async (int id, UpdatePropertyDto request, PropertyService propertyService, CancellationToken cancellationToken) =>
        {
            if (id <= 0)
            {
                return Results.BadRequest(new { Message = "ID måste vara ett positivt heltal." });
            }

            var errors = ValidateProperty(request.Name, request.Address, request.Floors, request.PortCode);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var property = new Property
            {
                Id = id,
                Name = request.Name,
                Address = request.Address,
                Floors = request.Floors,
                PortCode = request.PortCode
            };

            bool updated = await propertyService.UpdatePropertyAsync(property, cancellationToken);

            if (!updated)
            {
                return Results.NotFound(new { Message = "Fastigheten hittades inte." });
            }

            return Results.NoContent();
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateProperty(string? name, string? address, string? floors, string? portCode)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            errors["Name"] = ["Name must be no more than 100 characters."];
        }

        if (string.IsNullOrWhiteSpace(address) || address.Length > 200)
        {
            errors["Address"] = ["Address must be no more than 200 characters."];
        }

        if (!int.TryParse(floors, out int floorCount) || floorCount > 25)
        {
            errors["Floors"] = ["Floors must be an integer between 1 and 25."];
        }

        if (string.IsNullOrWhiteSpace(portCode) || portCode.Length > 12)
        {
            errors["PortCode"] = ["Port code is required and must be between 1 and 12 characters."];
        }

        return errors;
    }
}
