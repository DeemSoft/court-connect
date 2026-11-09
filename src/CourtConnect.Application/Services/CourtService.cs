using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Mapping;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Services;

public sealed class CourtService(ICourtRepository courts)
{
    public async Task<CourtDto> CreateAsync(CreateCourtRequest request, CancellationToken cancellationToken)
    {
        var court = new Court(request.Name, request.Location);
        await courts.AddAsync(court, cancellationToken);
        await courts.SaveChangesAsync(cancellationToken);

        return DtoMapper.ToDto(court);
    }
}
