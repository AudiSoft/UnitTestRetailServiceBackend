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
    public class ProductsControllerTests
    {
        private  RetailDatabaseContext _context;
        private  Mock<ILogger<ProductsController>> _mockLogger;
        private  Mock<IConnectionStringResolver> _mockConnectionStringResolver;
        private  ProductsController _controller;
        private static int idCounter = 1;
        private String connectionString = "InMemoryDb:ProductsDatabase";
        public ProductsControllerTests()
        {
            SetupContext();
        }
        private void SetupContext()
        { 
            _context = DbContextFactory.CreateDbContext(connectionString);
            _mockLogger = new Mock<ILogger<ProductsController>>();
            _mockConnectionStringResolver = new Mock<IConnectionStringResolver>();
            _controller = new ProductsController(_mockLogger.Object, _context, _mockConnectionStringResolver.Object);

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
            var company = new Company { Id = idCounter++, Name = "Example Company", GS1CompanyId = "12345", Logo = "http://example.com/logo.png", EPCSequence = "001", AuthMethod = AuthMethod.OAuth };
            _context.Companies.Add(company);

            var userAlice = new User { Name = "Alice", Email = "alice@example.com", Rol = Rol.Admin, CompanyId = company.Id };
            var userBob = new User { Name = "Bob", Email = "bob@example.com", Rol = Rol.User, CompanyId = company.Id };

            var product1 = new ProductSkus { Id = idCounter++, Name = "Product A", Description = "Description A", CompanyId = company.Id, Barcode = "123456789" };
            var product2 = new ProductSkus { Id = idCounter++, Name = "Product B", Description = "Description B", CompanyId = company.Id, Barcode = "987654321" };

            _context.Users.AddRange(userAlice, userBob);
            _context.ProductsSku.AddRange(product1, product2);

            _context.SaveChanges();
        }

        [Fact]
        public async Task GetProducts_ReturnsAllProductsForCompany()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Act
            var result = await _controller.GetProducts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsType<List<ProductSkus>>(okResult.Value);
            Assert.Equal(2, products.Count); // Assumes there are 2 products in the test data
            Assert.All(products, p => Assert.Equal(1, p.CompanyId));
        }

        [Fact]
        public async Task PostProduct_ReturnsBadRequest_WhenProductWithSameBarcodeExists()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Create a new product with the same barcode as an existing product
            var newProduct = new ProductSkus { Name = "Product C", Description = "Description C", CompanyId = 1, Barcode = "123456789" };

            // Act
            var result = await _controller.Post(newProduct);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("La referencia ya tiene el código de barras asignado", badRequestResult.Value);
        }

        [Fact]
        public async Task PostProduct_ReturnsBadRequest_WhenProductWithSameNameAndSupplierExists()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Create a new product with the same name, supplier and CompanyId as an existing product
            var existingProduct = _context.ProductsSku.First();
            var newProduct = new ProductSkus { Name = existingProduct.Name, Description = "Description C", CompanyId = existingProduct.CompanyId, SuppliersId = existingProduct.SuppliersId };

            // Act
            var result = await _controller.Post(newProduct);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("La referencia ya existe con el mismo nombre y proveedor", badRequestResult.Value);
        }

        [Fact]
        public async Task PostProduct_ReturnsOk_WhenProductIsCreatedSuccessfully()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            // Create a new product that does not exist in the database
            var existingProduct = _context.ProductsSku.First();
            var newProduct = new ProductSkus { Name = "Product C", Description = "Description C", CompanyId = existingProduct.CompanyId, SuppliersId = existingProduct.SuppliersId };

            // Act
            var result = await _controller.Post(newProduct);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal("Referencia creada exitosamente", okResult.Value);

            // Create a new context for the assertion
            using (var assertContext = DbContextFactory.CreateDbContext(connectionString))
            {
                // Check that the product was added to the database
                Assert.NotNull(assertContext.ProductsSku.FirstOrDefault(p => p.Name == newProduct.Name && p.CompanyId == newProduct.CompanyId && p.SuppliersId == newProduct.SuppliersId));
            }
        }
    }
}
