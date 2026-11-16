namespace DeemSoft.CourtConnect.Domain;

public sealed class Player
{
    // EF Core lo usa al materializar jugadores; el alta normal sigue validando los datos y la edad.
    private Player()
    {
    }

    // La fecha de alta se recibe explícitamente para que la validación sea determinista y fácil de probar.
    public Player(
        string nickname,
        string fullName,
        DateOnly dateOfBirth,
        string email,
        DateOnly registeredOn,
        Guid? identityUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (GetAgeOn(registeredOn, dateOfBirth) < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), "Los jugadores deben tener al menos 16 años.");
        }

        Nickname = nickname.Trim();
        FullName = fullName.Trim();
        DateOfBirth = dateOfBirth;
        Email = email.Trim();
        IdentityUserId = identityUserId;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Nickname { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public DateOnly DateOfBirth { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public Guid? IdentityUserId { get; private set; }

    public void LinkAccount(Guid identityUserId)
    {
        if (IdentityUserId is Guid existingUserId && existingUserId != identityUserId)
        {
            throw new InvalidOperationException("El jugador ya está vinculado a otra cuenta.");
        }

        IdentityUserId = identityUserId;
    }

    // Hace el cálculo de edad teniendo en cuenta el cumpleaños real en esa fecha.
    public int GetAgeOn(DateOnly comparisonDate)
    {
        return GetAgeOn(comparisonDate, DateOfBirth);
    }

    private static int GetAgeOn(DateOnly comparisonDate, DateOnly dateOfBirth)
    {
        var age = comparisonDate.Year - dateOfBirth.Year;

        if (comparisonDate < dateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }
}
