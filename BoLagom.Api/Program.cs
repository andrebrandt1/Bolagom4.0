using BoLagom.Api.Endpoints;
using BoLagom.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<ApartmentService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.AddPropertyEndpoints();
app.AddApartmentEndpoints();

app.Run();
