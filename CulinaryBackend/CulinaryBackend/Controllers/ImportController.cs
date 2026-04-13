using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportController : ControllerBase
    {
        private readonly PoiService _poiService;

        public ImportController(PoiService poiService)
        {
            _poiService = poiService;
        }

        [HttpPost("pois")]
        public async Task<IActionResult> ImportPois()
        {
            try
            {
                var jsonPath = @"E:\SP_Admin\CulinaryProject\CulinaryDB.Pois.json";
                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
                var pois = JsonSerializer.Deserialize<List<Poi>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (pois == null || pois.Count == 0)
                    return BadRequest("No POIs found in JSON file");

                // Delete all existing POIs
                var existing = await _poiService.GetAsync();
                foreach (var poi in existing)
                {
                    await _poiService.DeleteAsync(poi.Id!);
                }

                // Insert new POIs
                foreach (var poi in pois)
                {
                    await _poiService.CreateAsync(poi);
                }

                return Ok(new { message = $"Successfully imported {pois.Count} POIs", count = pois.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
