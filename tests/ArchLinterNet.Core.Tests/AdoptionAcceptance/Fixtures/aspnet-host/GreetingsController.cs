using Microsoft.AspNetCore.Mvc;

namespace Synthetic.AspNetHost;

[ApiController]
[Route("greetings")]
public sealed class GreetingsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("hello");
}
