using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.Dotnet.Controllers.Tests.Controllers
{
    [Collection("Sequential")]
    public class OrdersControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _mockNotificationService;

        public OrdersControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _mockNotificationService = new Mock<INotificationService>();

            _factory = factory.WithWebHostBuilder(builder =>
            {
                _ = builder.ConfigureServices(services =>
                {
                    _ = services.AddSingleton(_mockNotificationService.Object);

                    ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                    {
                        _ = services.Remove(descriptor);
                    }

                    // Add ApplicationDbContext using an in-memory database for testing
                    _ = services.AddDbContext<AppDbContext>(options =>
                    {
                        _ = options.UseInMemoryDatabase($"InMemoryDbForTesting-{GetType()}");
                    });
                    _ = services.AddScoped((_sp) => _mockNotificationService.Object);


                    ServiceProvider sp = services.BuildServiceProvider();
                });
            });

            IServiceScope scope = _factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task ProcessOrderShouldReturn()
        {
            HttpClient client = _factory.CreateClient();

            List<Product> allProducts = CreateProducts();
            HashSet<Product> orderItems = new(allProducts);
            Order order = CreateOrder(orderItems);
            await _context.Products.AddRangeAsync(allProducts);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            HttpResponseMessage response = await client.PostAsync($"/orders/{order.Id}/processOrder", null);
            _ = response.EnsureSuccessStatusCode();

            Order? resultOrder = await _context.Orders.FindAsync(order.Id);
            Assert.Equal(resultOrder.Id, order.Id);
        }

        [Fact]
        public async Task NormalProductInStockShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "NORMAL", Name = "USB Cable" });

            Assert.Equal(29, result.Available);
            Assert.Equal(15, result.LeadTime);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NormalProductOutOfStockShouldNotifyDelay()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 10, Available = 0, Type = "NORMAL", Name = "USB Dongle" });

            Assert.Equal(0, result.Available);
            Assert.Equal(10, result.LeadTime);
            _mockNotificationService.Verify(n => n.SendDelayNotification(10, "USB Dongle"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NormalProductOutOfStockWithoutLeadTimeShouldNotNotify()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 0, Available = 0, Type = "NORMAL", Name = "USB Dongle" });

            Assert.Equal(0, result.Available);
            Assert.Equal(0, result.LeadTime);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpirableProductNotExpiredShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = DateTime.Today.AddDays(26) });

            Assert.Equal(29, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpirableProductExpiringTomorrowShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = DateTime.Today.AddDays(1) });

            Assert.Equal(29, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpirableProductExpiringLaterTodayShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = DateTime.Today.AddHours(23).AddMinutes(59) });

            Assert.Equal(29, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpirableProductExpiringTodayAtMidnightShouldBeExpired()
        {
            DateTime expiryDate = DateTime.Today;

            Product result = await ProcessOrderWith(new Product { LeadTime = 90, Available = 6, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = expiryDate });

            Assert.Equal(0, result.Available);
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Milk", expiryDate), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpirableProductExpiredYesterdayShouldBeExpired()
        {
            DateTime expiryDate = DateTime.Today.AddDays(-1);

            Product result = await ProcessOrderWith(new Product { LeadTime = 90, Available = 6, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = expiryDate });

            Assert.Equal(0, result.Available);
            Assert.Equal(90, result.LeadTime);
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Milk", expiryDate), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpirableProductOutOfStockShouldNotifyExpiration()
        {
            DateTime expiryDate = DateTime.Today.AddDays(26);

            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 0, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = expiryDate });

            Assert.Equal(0, result.Available);
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Butter", expiryDate), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductInSeasonShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-2), SeasonEndDate = DateTime.Today.AddDays(58) });

            Assert.Equal(29, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductSeasonStartedYesterdayShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-1), SeasonEndDate = DateTime.Today.AddDays(58) });

            Assert.Equal(29, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductSeasonStartingTodayShouldNotifyDelay()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today, SeasonEndDate = DateTime.Today.AddDays(58) });

            Assert.Equal(30, result.Available);
            _mockNotificationService.Verify(n => n.SendDelayNotification(15, "Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductSeasonEndingTomorrowShouldBeDecremented()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 0, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-30), SeasonEndDate = DateTime.Today.AddDays(1) });

            Assert.Equal(29, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductSeasonEndingTodayShouldBeOutOfStock()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 0, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-30), SeasonEndDate = DateTime.Today });

            Assert.Equal(0, result.Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductSeasonEndedYesterdayShouldBeOutOfStock()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-30), SeasonEndDate = DateTime.Today.AddDays(-1) });

            Assert.Equal(0, result.Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductBeforeSeasonShouldNotifyOutOfStockAndKeepStock()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Grapes", SeasonStartDate = DateTime.Today.AddDays(180), SeasonEndDate = DateTime.Today.AddDays(240) });

            Assert.Equal(30, result.Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Grapes"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductOutOfStockRestockedTheDayBeforeSeasonEndShouldNotifyDelay()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 9, Available = 0, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-2), SeasonEndDate = DateTime.Today.AddDays(10) });

            Assert.Equal(0, result.Available);
            Assert.Equal(9, result.LeadTime);
            _mockNotificationService.Verify(n => n.SendDelayNotification(9, "Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SeasonalProductOutOfStockRestockedOnSeasonEndDayShouldBeOutOfStock()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 10, Available = 0, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Today.AddDays(-2), SeasonEndDate = DateTime.Today.AddDays(10) });

            Assert.Equal(0, result.Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UnknownProductTypeShouldBeIgnored()
        {
            Product result = await ProcessOrderWith(new Product { LeadTime = 15, Available = 30, Type = "UNKNOWN", Name = "Mystery" });

            Assert.Equal(30, result.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessUnknownOrderShouldFail()
        {
            HttpResponseMessage response = await _factory.CreateClient().PostAsync("/orders/12345/processOrder", null);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        private async Task<Product> ProcessOrderWith(Product product)
        {
            Order order = CreateOrder(new HashSet<Product> { product });
            _ = await _context.Products.AddAsync(product);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            HttpResponseMessage response = await _factory.CreateClient().PostAsync($"/orders/{order.Id}/processOrder", null);
            _ = response.EnsureSuccessStatusCode();

            return await _context.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        }

        private static Order CreateOrder(HashSet<Product> products)
        {
            return new Order { Items = products };
        }

        private static List<Product> CreateProducts()
        {
            return new List<Product>
            {
                new Product { LeadTime = 15, Available = 30, Type = "NORMAL", Name = "USB Cable" },
                new Product { LeadTime = 10, Available = 0, Type = "NORMAL", Name = "USB Dongle" },
                new Product { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = DateTime.Now.AddDays(26) },
                new Product { LeadTime = 90, Available = 6, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = DateTime.Now.AddDays(-2) },
                new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Now.AddDays(-2), SeasonEndDate = DateTime.Now.AddDays(58) },
                new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Grapes", SeasonStartDate = DateTime.Now.AddDays(180), SeasonEndDate = DateTime.Now.AddDays(240) }
            };
        }
    }
}
