namespace DeemSoft.CourtConnect.Domain;

public enum GameStatus
{
    Draft,
    Open,
    Closed
}

public sealed class Game
{
    // EF Core lo usa al materializar partidos desde la base de datos.
    private Game()
    {
    }

    // El partido gestiona la lista de inscripciones y mantiene las plazas disponibles.
    private readonly List<Registration> _registrations = [];

    public Game(
        Court court,
        DateTimeOffset startAt,
        TimeSpan duration,
        int capacity,
        int minimumAge,
        decimal pricePerPlayer,
        GameStatus status = GameStatus.Open,
        Guid? organizerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(court);

        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "El partido debe tener al menos 2 plazas.");
        }

        if (minimumAge < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAge), "La edad mínima no puede ser inferior a 16 años.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "La duración debe ser mayor que cero.");
        }

        // El precio puede ser cero para una cancha pública, pero nunca negativo.
        if (pricePerPlayer < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pricePerPlayer), "El precio por jugador no puede ser negativo.");
        }

        // Evita aceptar valores numéricos que no correspondan a ningún estado definido.
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "El estado del partido no es válido.");
        }

        Court = court;
        StartAt = startAt;
        Duration = duration;
        Capacity = capacity;
        MinimumAge = minimumAge;
        PricePerPlayer = pricePerPlayer;
        Status = status;
        OrganizerUserId = organizerUserId;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Court Court { get; private set; } = null!;

    public DateTimeOffset StartAt { get; private set; }

    public TimeSpan Duration { get; private set; }

    public int Capacity { get; private set; }

    public int MinimumAge { get; private set; }

    public decimal PricePerPlayer { get; private set; }

    public GameStatus Status { get; private set; }

    public Guid? OrganizerUserId { get; private set; }

    public IReadOnlyCollection<Registration> Registrations => _registrations.AsReadOnly();

    public bool IsOrganizedBy(Guid userId)
    {
        return OrganizerUserId == userId;
    }

    public void UpdateDetails(
        DateTimeOffset startAt,
        TimeSpan duration,
        int capacity,
        int minimumAge,
        decimal pricePerPlayer)
    {
        ValidateDetails(duration, capacity, minimumAge, pricePerPlayer);

        StartAt = startAt;
        Duration = duration;
        Capacity = capacity;
        MinimumAge = minimumAge;
        PricePerPlayer = pricePerPlayer;
    }

    public void SetStatus(GameStatus status)
    {
        if (status is not (GameStatus.Open or GameStatus.Closed))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "El partido solo se puede abrir o cerrar.");
        }

        Status = status;
    }

    // Un jugador solo puede apuntarse mientras el partido está abierto y antes de que empiece.
    public Registration Register(Player player, DateTimeOffset registrationDate)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Status != GameStatus.Open)
        {
            throw new InvalidOperationException("Solo se admiten inscripciones en partidos abiertos.");
        }

        if (registrationDate >= StartAt)
        {
            throw new InvalidOperationException("La inscripción debe realizarse antes de que empiece el partido.");
        }

        // DateTime conserva la hora local del desplazamiento de StartAt para calcular la edad en la fecha del partido.
        if (player.GetAgeOn(DateOnly.FromDateTime(StartAt.DateTime)) < MinimumAge)
        {
            throw new InvalidOperationException("El jugador no cumple la edad mínima para este partido.");
        }

        if (CountActiveRegistrations() >= Capacity)
        {
            throw new InvalidOperationException("El partido ha alcanzado el número máximo de inscripciones activas.");
        }

        if (HasActiveRegistrationFor(player))
        {
            throw new InvalidOperationException("El jugador ya está inscrito en este partido.");
        }

        var registration = new Registration(this, player, registrationDate, PricePerPlayer);
        _registrations.Add(registration);
        return registration;
    }

    // Cancelar una inscripción libera la plaza, pero solo antes de que empiece el partido y solo si pertenece a este juego.
    public void CancelRegistration(Registration registration, DateTimeOffset cancellationDate)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.GameId != Id)
        {
            throw new InvalidOperationException("La inscripción no pertenece a este partido.");
        }

        if (cancellationDate >= StartAt)
        {
            throw new InvalidOperationException("Las inscripciones solo se pueden cancelar antes de que empiece el partido.");
        }

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            throw new InvalidOperationException("La inscripción ya está cancelada.");
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

    private static void ValidateDetails(TimeSpan duration, int capacity, int minimumAge, decimal pricePerPlayer)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "El partido debe tener al menos 2 plazas.");
        }

        if (minimumAge < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAge), "La edad mínima no puede ser inferior a 16 años.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "La duración debe ser mayor que cero.");
        }

        if (pricePerPlayer < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pricePerPlayer), "El precio por jugador no puede ser negativo.");
        }
    }
}
