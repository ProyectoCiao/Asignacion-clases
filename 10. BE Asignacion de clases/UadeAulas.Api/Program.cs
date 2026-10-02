using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using UadeAulas.Api.Data;
using UadeAulas.Api.Services;
using UadeAulas.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddScoped<Repository>();
builder.Services.AddScoped<OfertaService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<GrabarService>();
builder.Services.AddScoped<SugerirService>();
builder.Services.AddScoped<DemandaService>();
builder.Services.AddScoped<MapaService>();
builder.Services.AddScoped<ClaseService>();
builder.Services.AddScoped<PlanificacionService>();

// Add CORS
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:4200";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON serialization (camelCase)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Errores no controlados: el middleware los registra en el log y el cliente recibe { error } como el resto
// de las respuestas de error. Los parámetros inválidos/faltantes siguen siendo 400.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = ex is BadHttpRequestException bad
        ? bad.StatusCode
        : StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "Error interno del servidor." });
}));

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // En desarrollo el front usa el proxy HTTP de Angular; redirigir a HTTPS lo rompería
    app.UseHttpsRedirection();
}

app.UseCors();

// Map endpoints
app.MapOfertaEndpoints();
app.MapCatalogEndpoints();
app.MapCuadreEndpoints();
app.MapDemandaEndpoints();
app.MapMapaClaseEndpoints();
app.MapPlanificacionEndpoints();
app.MapPeriodoEndpoints();

// Health check endpoint
app.MapGet("/health", () => new { status = "healthy" })
    .WithName("Health");

// La URL (http://localhost:5080, la que espera el proxy del front) se configura en
// appsettings.json ("Urls") y en Properties/launchSettings.json
app.Run();
