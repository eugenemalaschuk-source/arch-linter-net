using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace Synthetic.AspNetHost;

[ApiController]
[Route("greetings")]
public sealed class GreetingsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("hello");
}

// Mirrors the host-bootstrap shape that caused the packaged Linux regression:
// the target assembly needs WebApplicationBuilder from Microsoft.AspNetCore.App
// while the analyzer reflects over it from an isolated load context.
public static class HostBootstrap
{
    public static void Configure(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        _ = builder.Logging;
    }
}
