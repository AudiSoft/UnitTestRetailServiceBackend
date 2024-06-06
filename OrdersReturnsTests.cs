using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RetailServiceBackend.Models;
using RetailServiceBackend.Controllers;
using Xunit;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RetailServiceBackend.Models.Enums;
using Moq;
using Microsoft.Extensions.Logging;
namespace UnitTestBackHenutsen
{
    public class OrdersReturnsTests
    {
        private  RetailDatabaseContext _context;
        private  Mock<ILogger<OrdersController>> _mockLogger;
        private  Mock<IConnectionStringResolver> _mockConnectionStringResolver;
        private OrdersController _controller;
        private static int idCounter = 1;
        private String connectionString = "InMemoryDb:OrderReturnsDatabase";
        public OrdersReturnsTests()
        {
            SetupContext();
        }
        private void SetupContext()
        { 
            _context = DbContextFactory.CreateDbContext(connectionString);
            _mockLogger = new Mock<ILogger<OrdersController>>();
            _mockConnectionStringResolver = new Mock<IConnectionStringResolver>();
            _controller = new OrdersController(_mockLogger.Object, _context, _mockConnectionStringResolver.Object);

            SetupTestData();
        }

        public void ResetDatabase()
        {
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
            _context.ChangeTracker.Clear();
            idCounter = 1;
            SetupTestData();
        }


        private void SetupTestData()
        {
            var company = new Company { Id = idCounter++, Name = "Example Company returns", GS1CompanyId = "12345", Logo = "http://example.com/logo.png", EPCSequence = "001", AuthMethod = AuthMethod.OAuth };
            _context.Companies.Add(company);

            var userAlice = new User { Id = idCounter++, Name = "Alice", Email = "aliceReturn@example.com", Rol = Rol.Admin, CompanyId = company.Id };
            var userBob = new User { Id = idCounter++, Name = "Bob", Email = "bobReturns@example.com", Rol = Rol.User, CompanyId = company.Id };

            var product1 = new ProductSkus { Id = idCounter++, Name = "Product AAA", Description = "Description A", CompanyId = company.Id, Barcode = "123456789" };
            var product2 = new ProductSkus { Id = idCounter++, Name = "Product BAA", Description = "Description B", CompanyId = company.Id, Barcode = "987654321" };

            var location1 = new Locations { Id = idCounter++, Name = "Location 1", CompanyId = company.Id };
            var location2 = new Locations { Id = idCounter++, Name = "Location 2", CompanyId = company.Id };

            var userLocation1 = new UserLocations { UserId = userAlice.Id, LocationsId = location1.Id };
            var userLocation2 = new UserLocations { UserId = userAlice.Id, LocationsId = location2.Id };
            _context.UserLocations.AddRange(userLocation1, userLocation2);

            var productInstance1 = new ProductInstances { Id = idCounter++, ProductSkusId = product1.Id, Serial = "123456789", EPC = "123456789012345678901234", LegacyCode = "23456789", LocationId = location1.Id };
            var productInstance2 = new ProductInstances { Id = idCounter++, ProductSkusId = product2.Id, Serial = "987654321", EPC = "987654321098765432109876", LegacyCode = "87654321", LocationId = location2.Id };
            var productInstance3 = new ProductInstances { Id = idCounter++, ProductSkusId = product1.Id, Serial = "901234211", EPC = "123456789555555555555555", LegacyCode = "32234444", LocationId = location1.Id };


            var status1 = new Status { Id = idCounter++, Name = "Completado" };
            var status2 = new Status { Id = idCounter++, Name = "En proceso" };

            var typeMovement1 = new TypeMovements { Id = idCounter++, Name = "Returns", Initials = "RE",CompanyId = company.Id };
            _context.TypeMovements.Add(typeMovement1);

            var order1 = new Orders { Id = idCounter++, UserId = userAlice.Id, StatusId = status1.Id, TypeMovementId = typeMovement1.Id, Destination = location1.Name };

            var movement1 = new ProductMovements { Id = idCounter++, OrdersId = order1.Id, ProductInstanceId = productInstance1.Id, StatusId = status1.Id,Status=status1 };
            var movement2 = new ProductMovements { Id = idCounter++, OrdersId = order1.Id, ProductInstanceId = productInstance2.Id, StatusId = status1.Id, Status = status1 };
            var movement3 = new ProductMovements { Id = idCounter++, OrdersId = order1.Id, ProductInstanceId = productInstance3.Id, StatusId = status2.Id, Status = status2 };

            _context.Users.AddRange(userAlice, userBob);
            _context.ProductsSku.AddRange(product1, product2);
            _context.Locations.AddRange(location1, location2);
            _context.ProductInstances.AddRange(productInstance1, productInstance2, productInstance3);
            _context.Orders.Add(order1);
            _context.ProductMovements.AddRange(movement1, movement2,movement3);

            _context.SaveChanges();
        }

        [Fact]
        public async Task GetReturnsCompleted_ReturnsCorrectProductMovements()
        {
            // Arrange
            var email = "aliceReturn@example.com";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = email;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockConnectionStringResolver.Setup(m => m.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Act
            var result = await _controller.GetReturnsCompleted();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var productMovements = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);

            Assert.NotEmpty(productMovements);

            var firstMovement = productMovements.First();

            var ordersProperty = firstMovement.GetType().GetProperty("Orders");
            var productInstanceProperty = firstMovement.GetType().GetProperty("ProductInstance");

            Assert.NotNull(ordersProperty);
            Assert.NotNull(productInstanceProperty);

            var order = ordersProperty.GetValue(firstMovement);
            var productInstance = productInstanceProperty.GetValue(firstMovement);

            var orderDestinationProperty = order.GetType().GetProperty("Destination");
            var productInstanceSerialProperty = productInstance.GetType().GetProperty("Serial");

            Assert.NotNull(orderDestinationProperty);
            Assert.NotNull(productInstanceSerialProperty);

            Assert.Equal("Location 1", orderDestinationProperty.GetValue(order));
            Assert.Equal("123456789", productInstanceSerialProperty.GetValue(productInstance));
        }

        [Fact]
        public async Task GetReturnsInProcess_ReturnsCorrectProductMovements()
        {
            // Arrange
            var email = "aliceReturn@example.com";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = email;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockConnectionStringResolver.Setup(m => m.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Act
            var result = await _controller.GetReturnsInProcess();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var productMovements = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);

            Assert.NotEmpty(productMovements);

            var firstMovement = productMovements.First();

            var ordersProperty = firstMovement.GetType().GetProperty("Orders");
            var productInstanceProperty = firstMovement.GetType().GetProperty("ProductInstance");
            var statusProperty = firstMovement.GetType().GetProperty("Status");

            Assert.NotNull(ordersProperty);
            Assert.NotNull(productInstanceProperty);
            Assert.NotNull(statusProperty);

            var order = ordersProperty.GetValue(firstMovement);
            var productInstance = productInstanceProperty.GetValue(firstMovement);
            var status = statusProperty.GetValue(firstMovement);

            var orderDestinationProperty = order.GetType().GetProperty("Destination");
            var productInstanceSerialProperty = productInstance.GetType().GetProperty("Serial");
            var statusNameProperty = status.GetType().GetProperty("Name");

            Assert.NotNull(orderDestinationProperty);
            Assert.NotNull(productInstanceSerialProperty);
            Assert.NotNull(statusNameProperty);

            // check values of the properties of the first movement in the list of product movements returned
            Assert.Equal("Location 1", orderDestinationProperty.GetValue(order));
            Assert.Equal("901234211", productInstanceSerialProperty.GetValue(productInstance));
            Assert.Equal("En proceso", statusNameProperty.GetValue(status));
        }

        [Fact]
        public async Task UpdateProductMovementStatus_ReturnsOkResult_WhenUpdateIsSuccessful()
        {
            // Arrange
            var email = "aliceReturn@example.com";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = email;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockConnectionStringResolver.Setup(m => m.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Retrieve the product movement to update
            var existingProductMovement = _context.ProductMovements.Include(pm => pm.ProductInstance).First(pm => pm.ProductInstance.Serial == "123456789");
            var productMovement = new ProductMovements
            {
                Id = existingProductMovement.Id,
                Date = DateTime.UtcNow,
                ProductInstance = new ProductInstances
                {
                    Description = "Updated Description",
                    Observation = "Updated Observation",
                    Serial = "Updated Serial",
                    LegacyCode = "Updated LegacyCode",
                    Position = "Updated Position",
                    StatusProductInstanceId = existingProductMovement.ProductInstance.StatusProductInstanceId
                }
            };

            // Act
            var result = await _controller.UpdateProductMovementStatus(productMovement);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Product movement updated successfully.", okResult.Value);
        }

        [Fact]
        public async Task UpdateProductMovementStatus_ReturnsNotFound_WhenProductMovementNotFound()
        {
            // Arrange
            var email = "aliceReturn@example.com";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = email;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockConnectionStringResolver.Setup(m => m.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Create a product movement with a non-existing ID
            var productMovement = new ProductMovements
            {
                Id = 9999, // ID that does not exist
                Date = DateTime.UtcNow,
                ProductInstance = new ProductInstances
                {
                    Description = "Updated Description",
                    Observation = "Updated Observation",
                    Serial = "Updated Serial",
                    LegacyCode = "Updated LegacyCode",
                    Position = "Updated Position",
                    StatusProductInstanceId = 1 // Assuming this is a valid ID
                }
            };

            // Act
            var result = await _controller.UpdateProductMovementStatus(productMovement);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Product movement not found.", notFoundResult.Value);
        }

    }
}
