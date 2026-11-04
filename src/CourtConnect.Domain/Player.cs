namespace DeemSoft.CourtConnect.Domain;

public sealed class Player
{
    // La fecha de alta se recibe explícitamente para que la validación sea determinista y fácil de probar.
    public Player(string nickname, string fullName, DateOnly dateOfBirth, string email, DateOnly registeredOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (GetAgeOn(registeredOn, dateOfBirth) < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), "Players must be at least 16 years old.");
        }

        Nickname = nickname.Trim();
        FullName = fullName.Trim();
        DateOfBirth = dateOfBirth;
        Email = email.Trim();
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Nickname { get; private set; }

    public string FullName { get; private set; }

    public DateOnly DateOfBirth { get; private set; }

    public string Email { get; private set; }

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
