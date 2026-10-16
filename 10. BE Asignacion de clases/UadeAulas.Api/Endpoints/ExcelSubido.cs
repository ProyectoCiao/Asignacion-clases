using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

/// <summary>Lee el Excel (.xlsx) que llega en un formulario multipart; si algo falla devuelve el 400 listo para responder.</summary>
public static class ExcelSubido
{
    private const long MaxArchivoBytes = 10 * 1024 * 1024; // 10 MB

    public static async Task<(ExcelCrudo? Excel, string? Archivo, IResult? Error)> LeerAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
            return (null, null, Results.BadRequest(new { error = "Se esperaba un formulario multipart/form-data con el archivo Excel." }));

        var form = await request.ReadFormAsync();
        var archivo = form.Files["archivo"] ?? form.Files.FirstOrDefault();
        if (archivo == null || archivo.Length == 0)
            return (null, null, Results.BadRequest(new { error = "No se recibió ningún archivo Excel." }));
        if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, null, Results.BadRequest(new { error = "El archivo debe tener extensión .xlsx." }));
        if (archivo.Length > MaxArchivoBytes)
            return (null, null, Results.BadRequest(new { error = "El archivo supera el tamaño máximo permitido (10 MB)." }));

        try
        {
            using var stream = archivo.OpenReadStream();
            return (ExcelCrudoService.Leer(stream), archivo.FileName, null);
        }
        catch (ExcelInvalidoException ex)
        {
            return (null, null, Results.BadRequest(new { error = ex.Message }));
        }
    }
}
