// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Trace;
using cartservice.cartstore;
using cartservice.featureflags;
using Oteldemo;
using Microsoft.Extensions.Logging;

namespace cartservice.services
{
    public class CartService : Oteldemo.CartService.CartServiceBase
    {
        private readonly static Empty Empty = new Empty();
        private readonly static ICartStore BadCartStore = new RedisCartStore("badhost:1234");
        private readonly ICartStore _cartStore;
        private readonly FeatureFlagHelper _featureFlagHelper;
        private readonly ILogger<CartService> _logger;

        public CartService(ICartStore cartStore, FeatureFlagHelper featureFlagService, ILogger<CartService> logger)
        {
            _cartStore = cartStore;
            _featureFlagHelper = featureFlagService;
            _logger = logger;
        }

        public async override Task<Empty> AddItem(AddItemRequest request, ServerCallContext context)
        {
            var activity = Activity.Current;
            activity?.SetTag("app.user.id", request.UserId);
            activity?.SetTag("app.product.id", request.Item.ProductId);
            activity?.SetTag("app.product.quantity", request.Item.Quantity);
            _logger.LogInformation("Adding Item " + request.Item.ProductId + " for UserId:" + request.UserId);
            _logger.LogInformation("Neew");

            await _cartStore.AddItemAsync(request.UserId, request.Item.ProductId, request.Item.Quantity);
            return Empty;
        }

        public async override Task<Cart> GetCart(GetCartRequest request, ServerCallContext context)
        {
            var activity = Activity.Current;
            activity?.SetTag("app.user.id", request.UserId);
            activity?.AddEvent(new("Fetch cart"));

            var cart = await _cartStore.GetCartAsync(request.UserId);
            var totalCart = 0;
            foreach (var item in cart.Items)
            {
                totalCart += item.Quantity;
            }
            activity?.SetTag("app.cart.items.count", totalCart);
            _logger.LogInformation("Fetching Cart Items for UserId:" + request.UserId);

            return cart;
        }

        public async override Task<Empty> EmptyCart(EmptyCartRequest request, ServerCallContext context)
        {
            var activity = Activity.Current;
            activity?.SetTag("app.user.id", request.UserId);
            activity?.AddEvent(new("Empty cart"));

            try
            {
                if (await _featureFlagHelper.GenerateCartError())
                {
                    _logger.LogError("Cart Service delete Failed");
                    await BadCartStore.EmptyCartAsync(request.UserId);
                }
                else
                {
                    _logger.LogInformation("Removing Cart items");
                    await _cartStore.EmptyCartAsync(request.UserId);
                }
            }
            catch (RpcException ex)
            {
                Activity.Current?.RecordException(ex);
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            return Empty;
        }
    }
}
