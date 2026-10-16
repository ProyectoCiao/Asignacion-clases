namespace UadeAulas.Api.Endpoints;

/// <summary>
/// Usuario del backoffice que hace el cambio, para la auditoría.
/// PROVISORIO: se toma del header X-Usuario hasta que exista login y la tabla de permisos.
/// </summary>
public static class UsuarioActual
{
    public const string Header = "X-Usuario";
    public const string SinIdentificar = "sin-identificar";

    public static string? Leer(HttpRequest request)
    {
        var valor = request.Headers[Header].FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    public static IResult FaltaUsuario() =>
        Results.BadRequest(new { error = $"Falta el header '{Header}' con el usuario que realiza el cambio." });
}
