namespace DeemSoft.CourtConnect.Domain;

public sealed class Court
{
    // Una cancha necesita un nombre y una ubicación para que el organizador pueda publicar el partido correctamente.
    public Court(string name, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        Name = name.Trim();
        Location = location.Trim();
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; private set; }

    public string Location { get; private set; }
}
