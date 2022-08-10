using msa_phase_2_backend.Models;
using msa_phase_2_backend.Models.DTO;
using msa_phase_2_backend.Controllers;
using System.Text.Json;
using NSubstitute;
using System.Net;
using System.Text;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace msa_phase_2_backend.testing
{
    [TestFixture]
    public class Tests
    {
        private IHttpClientFactory httpClientFactoryMock;
        private UserController controller;
        private UserContext userContext;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Read test Pokemon schema from JSON
            StreamReader r = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestFiles", "litten.json"));

            string json = r.ReadToEnd();

            // Substitute PokeApi HttpClient, which always returns "litten.json"
            // https://anthonygiretti.com/2018/09/06/how-to-unit-test-a-class-that-consumes-an-httpclient-with-ihttpclientfactory-in-asp-net-core/
            httpClientFactoryMock = Substitute.For<IHttpClientFactory>();

            var fakeHttpMessageHandler = new FakeHttpMessageHandler(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                // Return the litten.json stringified
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
            var fakeHttpClient = new HttpClient(fakeHttpMessageHandler)
            {
                // Set base address to avoid invalid Uri
                BaseAddress = new Uri("https://pokeapi.co/api/v2")
            };

            httpClientFactoryMock.CreateClient("pokeapi").Returns(fakeHttpClient);
        }

        [SetUp]
        public void Setup()
        {
            // Use EF Core in memory database for testing. New database on every test
            // Guid is virtually unique
            var options = new DbContextOptionsBuilder<UserContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            userContext = new UserContext(options);

            userContext.Users.Add(new User { UserName = "Bianca" });
            userContext.Users.Add(new User { UserName = "Ralph" });
            userContext.Users.Add(new User { UserName = "Skyla" });
            userContext.SaveChanges();

            // Set up mock logger
            var mockLogger = new Mock<ILogger<UserController>>();

            controller = new UserController(userContext, mockLogger.Object, httpClientFactoryMock);

            //// Set up mock context
            //var userMockSet = new Mock<DbSet<User>>();
            //var pokemonMockSet = new Mock<DbSet<Pokemon>>();

            //var mockContext = new Mock<UserContext>();

            //// Users attribute returns DbSet<User>
            //mockContext.Setup(m => m.Users).Returns(userMockSet.Object);
            //mockContext.Setup(m => m.Pokemon).Returns(pokemonMockSet.Object);
            //userMockSet.Setup(d => d.Include("Pokemon")).Returns(userMockSet.Object);
        }

        [Test]
        public async Task GetUserById_ReturnsOkObjectResult()
        {
            int userId = 1;
            var result = await controller.GetUser(userId);
            //Console.WriteLine(JsonSerializer.Serialize((result.Result as OkObjectResult)!.Value));
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());

        }

        [Test]
        public async Task GetAllUsers_ReturnsOkObjectResult()
        {
            var result = await controller.GetUsers();
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetAllUsers_ReturnsAllUsers()
        {
            var result = await controller.GetUsers();
            //Console.WriteLine(JsonSerializer.Serialize(result.Value));
            Assert.That((result.Value as ICollection<User>)!, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task CreateUser_ReturnsCreatedAtActionResult()
        {
            var result = await controller.PostUser(new UserCreateDTO { UserName = "Volkner" });
            Assert.That(result.Result, Is.InstanceOf<CreatedAtActionResult>());
        }

        [Test]
        public async Task CreateUser_ReturnsCreatedItem()
        {
            var result = await controller.PostUser(new UserCreateDTO { UserName = "Cheren" });

            var typedResult = result.Result as CreatedAtActionResult;
            var value = typedResult!.Value as User;

            //Console.WriteLine(JsonSerializer.Serialize(value));
            // UserName is correct
            Assert.That(value!.UserName, Is.EqualTo("Cheren"));
            // Pokemon List is initialised as empty
            Assert.IsAssignableFrom<List<Pokemon>>(value!.Pokemon);
            Assert.That(value.Pokemon!, Is.Empty);
            // UserId is some integer
            Assert.That(value!.UserId, Is.InstanceOf<int>());
        }

        [Test]
        public async Task AddPokemonToUser_ReturnsNoContentResult()
        {
            var result = await controller.AddPokemonToUser(2, "litten");
            Assert.That(result.Result, Is.InstanceOf<NoContentResult>());
        }

        [Test]
        public async Task AddPokemonToUser_AddsPokemonToUser()
        {
            int userId = 2;

            var result = await controller.AddPokemonToUser(userId, "litten");

            // Get user after Pokemon is added
            var updatedUser = await userContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            // Count of Pokemon under user should be 1
            Assert.That(updatedUser!.Pokemon, Has.Count.EqualTo(1));
            Assert.That(updatedUser!.Pokemon.First().Name, Is.EqualTo("litten"));
        }
        
    }
}