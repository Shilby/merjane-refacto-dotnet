using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.Impl;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class ProductServiceTests
    {
        private static readonly DateTime Now = new(2026, 6, 15, 14, 30, 0);
        private static readonly DateTime Today = Now.Date;

        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<AppDbContext> _mockDbContext;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockDbContext = new Mock<AppDbContext>();
            _ = _mockDbContext.Setup(x => x.Products).ReturnsDbSet(Array.Empty<Product>());
            _productService = new ProductService(_mockNotificationService.Object, _mockDbContext.Object);
        }

        [Fact]
        public void NotifyDelayShouldSaveAndSendDelayNotification()
        {
            // GIVEN
            Product product = new()
            {
                LeadTime = 15,
                Available = 0,
                Type = "NORMAL",
                Name = "RJ45 Cable"
            };

            // WHEN
            _productService.NotifyDelay(product.LeadTime, product);

            // THEN
            Assert.Equal(0, product.Available);
            Assert.Equal(15, product.LeadTime);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(product.LeadTime, product.Name), Times.Once());
        }

        [Fact]
        public void NormalProductInStockShouldBeSold()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "NORMAL", Name = "USB Cable" };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(29, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void NormalProductOutOfStockShouldNotifyDelay()
        {
            Product product = new() { LeadTime = 10, Available = 0, Type = "NORMAL", Name = "USB Dongle" };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            Assert.Equal(10, product.LeadTime);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(10, "USB Dongle"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void NormalProductOutOfStockWithoutLeadTimeShouldDoNothing()
        {
            Product product = new() { LeadTime = 0, Available = 0, Type = "NORMAL", Name = "USB Dongle" };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Never());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ExpirableProductExpiringTomorrowShouldBeSold()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = Today.AddDays(1) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(29, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ExpirableProductExpiringEarlierTodayShouldStillBeSold()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = Today.AddHours(10) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(29, product.Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ExpirableProductExpiringTodayAtMidnightShouldBeExpired()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = Today };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendExpirationNotification("Milk", Today), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ExpirableProductExpiredYesterdayShouldBeExpired()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = Today.AddDays(-1) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            _mockNotificationService.Verify(service => service.SendExpirationNotification("Milk", Today.AddDays(-1)), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ExpirableProductOutOfStockShouldNotifyExpiration()
        {
            Product product = new() { LeadTime = 15, Available = 0, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = Today.AddDays(26) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            _mockNotificationService.Verify(service => service.SendExpirationNotification("Butter", Today.AddDays(26)), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ExpirableProductWithoutExpiryDateShouldThrow()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter" };

            _ = Assert.Throws<InvalidOperationException>(() => _productService.ProcessOrderedProduct(product, Now));
        }

        [Fact]
        public void SeasonalProductInSeasonShouldBeSold()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = Today.AddDays(-1), SeasonEndDate = Today.AddDays(1) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(29, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductSeasonStartingTodayShouldNotifyDelay()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = Today, SeasonEndDate = Today.AddDays(58) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(30, product.Available);
            _mockNotificationService.Verify(service => service.SendDelayNotification(15, "Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductSeasonStartingLaterTodayShouldNotifyOutOfStockAndKeepStock()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = Today.AddHours(15), SeasonEndDate = Today.AddDays(58) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(30, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductBeforeSeasonShouldNotifyOutOfStockAndKeepStock()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Grapes", SeasonStartDate = Today.AddDays(180), SeasonEndDate = Today.AddDays(240) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(30, product.Available);
            _mockNotificationService.Verify(service => service.SendOutOfStockNotification("Grapes"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductSeasonEndingTodayShouldBeOutOfStock()
        {
            Product product = new() { LeadTime = 0, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = Today.AddDays(-30), SeasonEndDate = Today };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductRestockedExactlyAtSeasonEndShouldNotifyDelay()
        {
            Product product = new() { LeadTime = 10, Available = 0, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = Today.AddDays(-2), SeasonEndDate = Now.AddDays(10) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            Assert.Equal(10, product.LeadTime);
            _mockNotificationService.Verify(service => service.SendDelayNotification(10, "Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductRestockedAfterSeasonEndShouldBeOutOfStock()
        {
            Product product = new() { LeadTime = 10, Available = 0, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = Today.AddDays(-2), SeasonEndDate = Now.AddDays(10).AddSeconds(-1) };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(0, product.Available);
            _mockNotificationService.Verify(service => service.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonalProductWithoutSeasonDatesShouldNotifyDelay()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon" };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(30, product.Available);
            _mockNotificationService.Verify(service => service.SendDelayNotification(15, "Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void UnknownProductTypeShouldDoNothing()
        {
            Product product = new() { LeadTime = 15, Available = 30, Type = "UNKNOWN", Name = "Mystery" };

            _productService.ProcessOrderedProduct(product, Now);

            Assert.Equal(30, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Never());
            _mockNotificationService.VerifyNoOtherCalls();
        }
    }
}
