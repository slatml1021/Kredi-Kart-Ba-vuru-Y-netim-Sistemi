using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Route("api/address-catalog")]
[Authorize(Roles = "Officer,Manager")]
public sealed class AddressCatalogController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("streets")]
    public async Task<IActionResult> GetStreets(
        [FromQuery] int neighborhoodId,
        [FromQuery] string? search,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        if (neighborhoodId <= 0)
            return BadRequest(new { detail = "Geçerli bir mahalle seçilmelidir." });

        limit = Math.Clamp(limit, 1, 1000);
        var catalogPath = Path.Combine(environment.ContentRootPath, "Data", "address-catalog.db");
        if (!System.IO.File.Exists(catalogPath))
            return Problem("Ulusal sokak kataloğu bulunamadı.", statusCode: 503);

        var results = new List<StreetCatalogItem>();
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = catalogPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            }.ToString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(search)
            ? "SELECT Id, Name FROM Streets WHERE NeighborhoodId = $neighborhoodId ORDER BY Name COLLATE NOCASE LIMIT $limit;"
            : "SELECT Id, Name FROM Streets WHERE NeighborhoodId = $neighborhoodId AND Name LIKE $search ESCAPE '\\' ORDER BY Name COLLATE NOCASE LIMIT $limit;";
        command.Parameters.AddWithValue("$neighborhoodId", neighborhoodId);
        command.Parameters.AddWithValue("$limit", limit);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            command.Parameters.AddWithValue("$search", $"%{escaped}%");
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(new StreetCatalogItem(reader.GetInt32(0), reader.GetString(1)));

        return Ok(results);
    }

    private sealed record StreetCatalogItem(int Id, string Name);
}
