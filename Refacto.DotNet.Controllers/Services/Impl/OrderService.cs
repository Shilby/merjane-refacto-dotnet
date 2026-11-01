using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class OrderService
    {
        private readonly ProductService _productService;
        private readonly AppDbContext _dbContext;

        public OrderService(ProductService productService, AppDbContext dbContext)
        {
            _productService = productService;
            _dbContext = dbContext;
        }

        public void ProcessOrder(long orderId)
        {
            Order? orderToProcess = _dbContext.Orders
                .Include(order => order.Items)
                .SingleOrDefault(order => order.Id == orderId);

            DateTime now = DateTime.Now;
            foreach (Product product in orderToProcess.Items)
            {
                _productService.ProcessOrderedProduct(product, now);
            }
        }
    }
}
