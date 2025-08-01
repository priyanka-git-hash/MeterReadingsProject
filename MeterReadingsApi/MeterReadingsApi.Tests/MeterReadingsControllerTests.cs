using MeterReadingsApi.Controllers;
using MeterReadingsApi.Data;
using MeterReadingsApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MeterReadingsApi.Tests
{
    [TestFixture]
    public class MeterReadingsControllerTests
    {
        private AppDbContext _context;
        private MeterReadingsController _controller;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureDeleted(); // clear before each test
            _context.Database.EnsureCreated(); // seed again

            _controller = new MeterReadingsController(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        [Test]
        public async Task Upload_ValidCsv_ReturnsSuccess()
        {
            // Arrange - a valid CSV string
            var csv = new StringBuilder();
            csv.AppendLine("AccountId,MeterReadingDateTime,MeterReadValue");
            csv.AppendLine("2344,2024-07-01 10:00:00,12345");
            csv.AppendLine("2345,2024-07-01 10:01:00,54321");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Act
            var result = await _controller.Upload(file) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var response = result.Value as UploadResult;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.EqualTo(2));
            Assert.That(response.Failure, Is.EqualTo(0));
        }

        [Test]
        public async Task Upload_CsvWithInvalidAccount_ReturnsFailure()
        {
            var csv = new StringBuilder();
            csv.AppendLine("AccountId,MeterReadingDateTime,MeterReadValue");
            csv.AppendLine("9999,2024-07-01 10:00:00,12345");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "invalid.csv");

            var result = await _controller.Upload(file) as OkObjectResult;

            Assert.That(result, Is.Not.Null);
            var response = result.Value as UploadResult;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.EqualTo(0));
            Assert.That(response.Failure, Is.EqualTo(1));
        }
    }
}
