using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class OrderService
    {
        private readonly ProductService _productService;
        private readonly AppDbContext _ctx;

        public OrderService(ProductService productService, AppDbContext ctx)
        {
            _productService = productService;
            _ctx = ctx;
        }

        public void ProcessOrder(long orderId)
        {
            Order? order = _ctx.Orders
                .Include(o => o.Items)
                .SingleOrDefault(o => o.Id == orderId);

            DateTime now = DateTime.Now;
            foreach (Product product in order.Items)
            {
                _productService.ProcessOrderedProduct(product, now);
            }
        }
    }
}
