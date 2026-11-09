namespace DeemSoft.CourtConnect.Domain;

public sealed class Court
{
    // EF Core lo usa al leer la entidad; las nuevas canchas siguen pasando por el constructor validado.
    private Court()
    {
    }

    // Una cancha necesita un nombre y una ubicación para que el organizador pueda publicar el partido correctamente.
    public Court(string name, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        Name = name.Trim();
        Location = location.Trim();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;
}
