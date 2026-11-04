namespace DeemSoft.CourtConnect.Domain;

public enum GameStatus
{
    Draft,
    Open,
    Closed
}

public sealed class Game
{
    // El partido gestiona la lista de inscripciones y mantiene las plazas disponibles.
    private readonly List<Registration> _registrations = [];

    public Game(Court court, DateTime startAt, TimeSpan duration, int capacity, int minimumAge, decimal pricePerPlayer, GameStatus status = GameStatus.Open)
    {
        ArgumentNullException.ThrowIfNull(court);

        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "A game must have at least 2 slots.");
        }

        if (minimumAge < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAge), "The minimum allowed age cannot be lower than 16.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The duration must be positive.");
        }

        Court = court;
        StartAt = startAt;
        Duration = duration;
        Capacity = capacity;
        MinimumAge = minimumAge;
        PricePerPlayer = pricePerPlayer;
        Status = status;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public Court Court { get; }

    public DateTime StartAt { get; }

    public TimeSpan Duration { get; }

    public int Capacity { get; }

    public int MinimumAge { get; }

    public decimal PricePerPlayer { get; }

    public GameStatus Status { get; private set; }

    public IReadOnlyCollection<Registration> Registrations => _registrations.AsReadOnly();

    // Un jugador solo puede apuntarse mientras el partido está abierto y antes de que empiece.
    public Registration Register(Player player, DateTime registrationDate)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Status != GameStatus.Open)
        {
            throw new InvalidOperationException("Registrations are only allowed in open games.");
        }

        if (registrationDate >= StartAt)
        {
            throw new InvalidOperationException("Registrations must be created before the game starts.");
        }

        if (player.GetAgeOn(StartAt) < MinimumAge)
        {
            throw new InvalidOperationException("The player does not meet the minimum age for this game.");
        }

        if (CountActiveRegistrations() >= Capacity)
        {
            throw new InvalidOperationException("The game has reached its maximum number of active registrations.");
        }

        if (HasActiveRegistrationFor(player))
        {
            throw new InvalidOperationException("The player already has an active registration for this game.");
        }

        var registration = new Registration(this, player, registrationDate, PricePerPlayer);
        _registrations.Add(registration);
        return registration;
    }

    // Cancelar una inscripción libera la plaza, pero solo antes de que empiece el partido y solo si pertenece a este juego.
    public void CancelRegistration(Registration registration, DateTime cancellationDate)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.GameId != Id)
        {
            throw new InvalidOperationException("The registration does not belong to this game.");
        }

        if (cancellationDate >= StartAt)
        {
            throw new InvalidOperationException("Registrations can only be cancelled before the game starts.");
        }

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            throw new InvalidOperationException("The registration is already cancelled.");
        }

        registration.Cancel();
    }

    public int CountActiveRegistrations()
    {
        return _registrations.Count(registration => registration.Status == RegistrationStatus.Confirmed);
    }

    public bool HasActiveRegistrationFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return _registrations.Any(registration => registration.PlayerId == player.Id && registration.Status == RegistrationStatus.Confirmed);
    }
}
