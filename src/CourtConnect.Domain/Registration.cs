namespace DeemSoft.CourtConnect.Domain;

public enum RegistrationStatus
{
    Confirmed,
    Cancelled
}

public sealed class Registration
{
    // EF Core lo usa al materializar inscripciones desde la base de datos.
    private Registration()
    {
    }

    // La inscripción conecta un jugador con un partido concreto y conserva el importe registrado en el momento de apuntarse.
    internal Registration(Game game, Player player, DateTimeOffset createdAt, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(player);

        GameId = game.Id;
        PlayerId = player.Id;
        CreatedAt = createdAt;
        Amount = amount;
        Status = RegistrationStatus.Confirmed;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid GameId { get; private set; }

    public Guid PlayerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public decimal Amount { get; private set; }

    public RegistrationStatus Status { get; private set; }

    // Una inscripción cancelada deja libre la plaza, pero no puede volver a cancelarse otra vez.
    internal void Cancel()
    {
        if (Status == RegistrationStatus.Cancelled)
        {
            throw new InvalidOperationException("The registration is already cancelled.");
        }

        Status = RegistrationStatus.Cancelled;
    }
}
