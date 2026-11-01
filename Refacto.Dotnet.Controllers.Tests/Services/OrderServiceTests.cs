using Moq;
using Moq.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.Impl;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class OrderServiceTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<AppDbContext> _mockDbContext;
        private readonly OrderService _orderService;

        public OrderServiceTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockDbContext = new Mock<AppDbContext>();
            ProductService productService = new(_mockNotificationService.Object, _mockDbContext.Object);
            _orderService = new OrderService(productService, _mockDbContext.Object);
        }

        [Fact]
        public void ProcessOrderShouldProcessEveryProductOfTheOrder()
        {
            Product inStock = new() { LeadTime = 15, Available = 30, Type = "NORMAL", Name = "USB Cable" };
            Product outOfStock = new() { LeadTime = 10, Available = 0, Type = "NORMAL", Name = "USB Dongle" };
            GivenOrders(new Order { Id = 1, Items = new List<Product> { inStock, outOfStock } });

            _orderService.ProcessOrder(1);

            Assert.Equal(29, inStock.Available);
            Assert.Equal(0, outOfStock.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Exactly(2));
            _mockNotificationService.Verify(service => service.SendDelayNotification(10, "USB Dongle"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrderWithoutProductsShouldDoNothing()
        {
            GivenOrders(new Order { Id = 1, Items = new List<Product>() });

            _orderService.ProcessOrder(1);

            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Never());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessUnknownOrderShouldThrow()
        {
            GivenOrders(new Order { Id = 1, Items = new List<Product>() });

            _ = Assert.Throws<NullReferenceException>(() => _orderService.ProcessOrder(2));
        }

        private void GivenOrders(params Order[] orders)
        {
            _ = _mockDbContext.Setup(x => x.Orders).ReturnsDbSet(orders);
        }
    }
}
