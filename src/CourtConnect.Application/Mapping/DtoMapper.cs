using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Mapping;

public static class DtoMapper
{
    public static CourtDto ToDto(Court court)
    {
        return new CourtDto(court.Id, court.Name, court.Location);
    }

    public static GameDto ToDto(Game game)
    {
        var activeRegistrations = game.CountActiveRegistrations();

        // El DTO expone plazas disponibles, no la colección interna de inscripciones del dominio.
        return new GameDto(
            game.Id,
            game.Court.Id,
            game.Court.Name,
            game.StartAt,
            (int)game.Duration.TotalMinutes,
            game.Capacity,
            Math.Max(0, game.Capacity - activeRegistrations),
            game.MinimumAge,
            game.PricePerPlayer,
            game.Status.ToString());
    }

    public static PlayerDto ToDto(Player player)
    {
        return new PlayerDto(player.Id, player.Nickname, player.FullName, player.DateOfBirth, player.Email);
    }

    public static RegistrationDto ToDto(Registration registration)
    {
        return new RegistrationDto(
            registration.Id,
            registration.GameId,
            registration.PlayerId,
            registration.CreatedAt,
            registration.Amount,
            registration.Status.ToString());
    }
}
