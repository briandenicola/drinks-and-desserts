using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface IAgentService
{
    Task ProcessCaptureAsync(Capture capture);
}
