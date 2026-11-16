namespace DeemSoft.CourtConnect.Application.Exceptions;

public sealed class EntityNotFoundException(string entityName, Guid id)
    : Exception($"No se encontró {GetDisplayName(entityName)} con identificador '{id}'.")
{
    private static string GetDisplayName(string entityName) => entityName switch
    {
        "Game" => "el partido",
        "Court" => "la pista",
        "Player" => "el jugador",
        "Player profile" => "el perfil de jugador",
        "Registration" => "la inscripción",
        _ => "el recurso"
    };
}
