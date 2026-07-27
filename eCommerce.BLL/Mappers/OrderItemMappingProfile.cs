using AutoMapper;
using eCommerce.BLL.DTO.OrderItem;
using eCommerce.DAL.Entities;

namespace eCommerce.BLL.Mappers;

public class OrderItemMappingProfile : Profile
{
    public OrderItemMappingProfile()
    {
        CreateMap<OrderItemAddRequest, OrderItem>();

        CreateMap<OrderItemUpdateRequest, OrderItem>();

        CreateMap<OrderItem, OrderItemResponse>();
    }
}
