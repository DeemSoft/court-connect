using System.ComponentModel.DataAnnotations;

namespace DeemSoft.CourtConnect.Application.Dtos;

public sealed record CreateGameRequest(
    [property: Required] Guid? CourtId,
    [property: Required] DateTimeOffset StartAt,
    [property: Range(1, 1440)] int DurationMinutes,
    [property: Range(2, 1000)] int Capacity,
    [property: Range(16, 120)] int MinimumAge,
    [property: Range(typeof(decimal), "0", "1000000")] decimal PricePerPlayer);

public sealed record GameDto(
    Guid Id,
    Guid CourtId,
    string CourtName,
    DateTimeOffset StartAt,
    int DurationMinutes,
    int Capacity,
    int AvailableSlots,
    int MinimumAge,
    decimal PricePerPlayer,
    string Status);
