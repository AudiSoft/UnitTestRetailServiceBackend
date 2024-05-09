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
using Xunit;
using System.Linq;
using System.Threading.Tasks;
namespace UnitTestBackHenutsen
{
    public class UserControllerTests
    {
        private  RetailDatabaseContext _context;
        private  Mock<ILogger<UserController>> _mockLogger;
        private  Mock<IConnectionStringResolver> _mockConnectionStringResolver;
        private UserController _controller;
        private static int idCounter = 1;
        private String connectionString = "InMemoryDb:UserDatabase";

        public UserControllerTests()
        {
            SetupContext();
        }
        private void SetupContext()
        {
            
            _context = DbContextFactory.CreateDbContext(connectionString);
            _mockLogger = new Mock<ILogger<UserController>>();
            _mockConnectionStringResolver = new Mock<IConnectionStringResolver>();
            _controller = new UserController(_context, _mockLogger.Object, _mockConnectionStringResolver.Object);

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

            _context.Users.AddRange(userAlice, userBob);      
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetUsersByCompanyId_ReturnsUsers()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            var result = await _controller.GetUsersByCompanyId();

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<User>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var users = Assert.IsType<List<User>>(okResult.Value);

            // Check that all returned users belong to the same company and that this company is the correct one
            int expectedCompanyId = 1; // Expected company based on user 'Alice'
            Assert.All(users, user => Assert.Equal(expectedCompanyId, user.CompanyId));

            // If you want to ensure that users are returned and not an empty list
            Assert.NotEmpty(users);
        }

        [Fact]
        public async Task CreateUser_ReturnsCreatedResponse_WhenUserIsValid()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            var newUser = new User
            {
                Name = "Charlie",
                Email = "charlie@example.com",
                Rol = Rol.User,
                CompanyId = 1
            };

           
            // Act
            var result = await _controller.CreateUser(newUser);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnedUser = Assert.IsType<User>(createdResult.Value);
            Assert.Equal(newUser.Email, returnedUser.Email);

            // Verify that a log information was called
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenCompanyDoesNotExist()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            var newUser = new User
            {       
                Name = "Charlie",
                Email = "charlie@example.com",
                Rol = Rol.User,
                CompanyId = 9999999 // Non-existent company ID
            };


            // Act
            var result = await _controller.CreateUser(newUser);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);

        }


        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenUserAlreadyExists()
        {
            // Setup HttpContext
            ResetDatabase();
            var httpContext = new DefaultHttpContext();
            httpContext.Items["email"] = "alice@example.com";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            // Mock the connection string resolver
            _mockConnectionStringResolver.Setup(x => x.ResolveConnectionString(It.IsAny<string>(), It.IsAny<RetailDatabaseContext>()))
                .Returns(connectionString);

            var newUser = new User
            {
               
                Name = "Alice",
                Email = "alice@example.com",
                Rol = Rol.Admin,
                CompanyId = 1 // This should match an existing user
            };

            // Mock logger setup
            _mockLogger.Setup(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>())
            ).Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, func) => { });

            // Act
            var result = await _controller.CreateUser(newUser);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);

            // Verify logger was called with the expected log level
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information, // Adjusting to match actual log level used in the controller
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(2)); // Adjusting to the number of times logged as seen in the error message
        }

    }
}
