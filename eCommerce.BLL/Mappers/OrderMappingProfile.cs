using AutoMapper;
using eCommerce.BLL.DTO.Order;
using eCommerce.DAL.Entities;

namespace eCommerce.BLL.Mappers;

public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<OrderAddRequest, Order>();

        CreateMap<OrderUpdateRequest, Order>();

        CreateMap<Order, OrderResponse>();
    }
}
