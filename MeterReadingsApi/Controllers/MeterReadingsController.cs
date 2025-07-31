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

            int successCount = 0;
            int failureCount = 0;
            var failureReasons = new List<string>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string? line;
                bool isFirstLine = true;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isFirstLine)
                    {
                        isFirstLine = false; // skip header
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = line.Split(',');

                    if (columns.Length < 3)
                    {
                        failureCount++;
                        failureReasons.Add("Invalid column count");
                        continue;
                    }

                    if (!int.TryParse(columns[0], out int accountId) ||
                        !DateTime.TryParse(columns[1], out DateTime readDateTime) ||
                        !int.TryParse(columns[2], out int meterReadValue))
                    {
                        failureCount++;
                        failureReasons.Add($"Parsing error for line: {line}");
                        continue;
                    }

                    // 1. Check if Account exists
                    var accountExists = await _context.Accounts.AnyAsync(a => a.AccountId == accountId);
                    if (!accountExists)
                    {
                        failureCount++;
                        failureReasons.Add($"Account {accountId} does not exist");
                        continue;
                    }

                    // 2. Reject duplicate readings (same AccountId + MeterReadingDateTime)
                    var duplicateExists = await _context.MeterReadings
                        .AnyAsync(mr => mr.AccountId == accountId && mr.MeterReadingDateTime == readDateTime);
                    if (duplicateExists)
                    {
                        failureCount++;
                        failureReasons.Add($"Duplicate reading for Account {accountId} at {readDateTime}");
                        continue;
                    }

                    // 3. Validate meter reading value format: exactly 5 digits, no leading zeros
                    string readingStr = columns[2].Trim();
                    if (readingStr.Length != 5 || !int.TryParse(readingStr, out int readingNum) || readingStr.StartsWith("0"))
                    {
                        failureCount++;
                        failureReasons.Add($"Invalid meter reading value '{readingStr}' for Account {accountId}");
                        continue;
                    }

                    // 4. Prevent older reading: Ensure new reading is newer than all existing for this account
                    var hasOlderReading = await _context.MeterReadings
                        .AnyAsync(mr => mr.AccountId == accountId && readDateTime <= mr.MeterReadingDateTime);
                    if (hasOlderReading)
                    {
                        failureCount++;
                        failureReasons.Add($"Reading for Account {accountId} is not newer than existing readings");
                        continue;
                    }

                    // Passed all validations, add new reading
                    _context.MeterReadings.Add(new MeterReading
                    {
                        AccountId = accountId,
                        MeterReadingDateTime = readDateTime,
                        MeterReadValue = meterReadValue
                    });

                    successCount++;
                }
            }

            // Save all valid meter readings at once
            await _context.SaveChangesAsync();

            // Return result with success and failure counts plus failure reasons details
            return Ok(new
            {
                Success = successCount,
                Failure = failureCount,
                FailureReasons = failureReasons
            });
        }

    }
}

