using FindThatBook.API.Models;
using FindThatBook.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController(IBookSearchService bookSearchService, ILogger<BooksController> logger)
    : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult<SearchResponse>> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query must not be empty." });

        if (request.Query.Length > 500)
            return BadRequest(new { error = "Query must be 500 characters or fewer." });

        logger.LogInformation("Search request: {Query}", request.Query);

        var response = await bookSearchService.SearchAsync(request.Query.Trim());
        return Ok(response);
    }
}
