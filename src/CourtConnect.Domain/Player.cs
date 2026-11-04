namespace DeemSoft.CourtConnect.Domain;

public sealed class Player
{
    // Un jugador siempre debe tener datos mínimos válidos y cumplir la edad mínima de la plataforma.
    public Player(string nickname, string fullName, DateOnly dateOfBirth, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (GetAgeOn(DateTime.Today, dateOfBirth) < 16)
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

    // Calcula la edad del jugador en una fecha concreta, útil para validar partidos por edad.
    public int GetAgeOn(DateTime comparisonDate)
    {
        return GetAgeOn(comparisonDate, DateOfBirth);
    }

    // Hace el cálculo de edad teniendo en cuenta el cumpleaños real en esa fecha.
    private static int GetAgeOn(DateTime comparisonDate, DateOnly dateOfBirth)
    {
        var currentDate = DateOnly.FromDateTime(comparisonDate);
        var age = currentDate.Year - dateOfBirth.Year;

        if (currentDate < dateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }
}
