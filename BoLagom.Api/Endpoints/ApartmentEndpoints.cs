using BoLagom.Api.Models;
using BoLagom.Api.Services;
using Microsoft.Data.SqlClient;

namespace BoLagom.Api.Endpoints;

public static class ApartmentEndpoints
{
    public static WebApplication AddApartmentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/properties/{propertyId:int}/apartments", static async (
            int propertyId,
            PropertyService propertyService,
            ApartmentService apartmentService,
            ILogger<ApartmentService> logger,
            CancellationToken cancellationToken) =>
        {
            if (propertyId <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["PropertyId"] = ["Fastighetens ID måste vara ett positivt heltal."]
                });
            }

            try
            {
                if (!await propertyService.PropertyExistsAsync(propertyId, cancellationToken))
                {
                    return Results.NotFound(new { Message = "Fastigheten hittades inte." });
                }

                IEnumerable<Apartment> apartments = await apartmentService.GetApartmentsAsync(propertyId, cancellationToken);
                return Results.Ok(apartments.Select(apartment => new ApartmentDto
                {
                    Id = apartment.Id,
                    ApartmentNumber = apartment.ApartmentNumber,
                    LivingArea = apartment.LivingArea,
                    MonthlyRent = apartment.MonthlyRent,
                    PropertyId = apartment.PropertyId
                }));
            }
            catch (SqlException exception)
            {
                logger.LogError(exception, "Kunde inte hämta lägenheter i fastighet {PropertyId}.", propertyId);
                return Results.Problem("Ett databasfel inträffade. Lägenheterna kunde inte hämtas.");
            }
        });

        app.MapPost("/api/properties/{propertyId:int}/apartments", static async (
            int propertyId,
            CreateApartmentDto request,
            PropertyService propertyService,
            ApartmentService apartmentService,
            ILogger<ApartmentService> logger,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateApartment(propertyId, request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            try
            {
                if (!await propertyService.PropertyExistsAsync(propertyId, cancellationToken))
                {
                    return Results.NotFound(new { Message = "Fastigheten hittades inte." });
                }

                var apartment = new Apartment
                {
                    ApartmentNumber = request.ApartmentNumber!.Trim(),
                    LivingArea = request.LivingArea!.Value,
                    MonthlyRent = request.MonthlyRent!.Value,
                    PropertyId = propertyId
                };

                bool created = await apartmentService.CreateApartmentAsync(apartment, cancellationToken);
                if (!created)
                {
                    return Results.Problem("Lägenheten kunde inte skapas.");
                }

                return Results.Created($"/api/properties/{propertyId}/apartments", new ApartmentDto
                {
                    Id = apartment.Id,
                    ApartmentNumber = apartment.ApartmentNumber,
                    LivingArea = apartment.LivingArea,
                    MonthlyRent = apartment.MonthlyRent,
                    PropertyId = apartment.PropertyId
                });
            }
            catch (SqlException exception) when (exception.IsDuplicateApartmentNumber())
            {
                return Results.Conflict(new { Message = "Lägenhetsnumret finns redan i den valda fastigheten." });
            }
            catch (SqlException exception) when (exception.IsApartmentPropertyForeignKeyViolation())
            {
                return Results.NotFound(new { Message = "Fastigheten hittades inte." });
            }
            catch (SqlException exception)
            {
                logger.LogError(exception, "Kunde inte skapa lägenhet i fastighet {PropertyId}.", propertyId);
                return Results.Problem("Ett databasfel inträffade. Lägenheten kunde inte skapas.");
            }
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateApartment(int propertyId, CreateApartmentDto request)
    {
        var errors = new Dictionary<string, string[]>();

        if (propertyId <= 0)
        {
            errors["PropertyId"] = ["Fastighetens ID måste vara ett positivt heltal."];
        }

        if (string.IsNullOrWhiteSpace(request.ApartmentNumber) || request.ApartmentNumber.Trim().Length > 5)
        {
            errors["ApartmentNumber"] = ["Lägenhetsnummer måste anges och får vara högst fem tecken."];
        }

        if (request.LivingArea is not decimal livingArea || livingArea <= 0 || livingArea > 999.9m ||
            decimal.Round(livingArea, 1) != livingArea)
        {
            errors["LivingArea"] = ["Boyta måste vara större än 0 och högst 999,9 m², med högst en decimal."];
        }

        if (request.MonthlyRent is null or < 0)
        {
            errors["MonthlyRent"] = ["Månadshyra måste anges som ett heltal som är minst 0."];
        }

        return errors;
    }
}
