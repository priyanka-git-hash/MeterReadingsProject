using MeterReadingsApi.Data;
using MeterReadingsApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingsApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeterReadingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MeterReadingsController(AppDbContext context) { _context = context; }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var meterReadings = new List<MeterReading>();
            int successCount = 0, failureCount = 0;

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string? line;
                bool isFirst = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isFirst) { isFirst = false; continue; }
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var columns = line.Split(',');
                    if (columns.Length < 3) { failureCount++; continue; }

                    if (!int.TryParse(columns[0], out var accountId) ||
                        !DateTime.TryParse(columns[1], out var readTime) ||
                        !int.TryParse(columns[2], out var readValue))
                    {
                        failureCount++;
                        continue;
                    }

                    var account = await _context.Accounts.FindAsync(accountId);
                    if (account == null)
                    {
                        failureCount++;
                        continue;
                    }

                    var existing = await _context.MeterReadings.FirstOrDefaultAsync(
                        m => m.AccountId == accountId && m.MeterReadingDateTime == readTime
                    );
                    if (existing != null)
                    {
                        failureCount++;
                        continue;
                    }

                    _context.MeterReadings.Add(new MeterReading
                    {
                        AccountId = accountId,
                        MeterReadingDateTime = readTime,
                        MeterReadValue = readValue
                    });
                    successCount++;
                }

                await _context.SaveChangesAsync();
            }

            return Ok(new { Success = successCount, Failure = failureCount });
        }
    }
}

