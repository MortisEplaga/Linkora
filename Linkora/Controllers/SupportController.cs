using Linkora.Repositories;
using Microsoft.AspNetCore.Mvc;
using Linkora.Models;

[Route("api/[controller]")]
[ApiController]
public class SupportController : ControllerBase
{
    private readonly ISupportRepository _supportRepository;

    public SupportController(ISupportRepository supportRepository)
    {
        _supportRepository = supportRepository;
    }

    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactSupport([FromBody] SupportRequestDto model)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "Invalid data" });

        return Ok(new { success = true, id = await _supportRepository.CreateRequestAsync(model.Name, model.Email, model.Phone, model.Message, User.Identity.IsAuthenticated ? User.GetUserId().ToString() : null) });
    }
}
