using System.Net;

namespace DeemSoft.CourtConnect.Ui.Components.Api;

public sealed class CourtConnectApiException(
    HttpStatusCode statusCode,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
