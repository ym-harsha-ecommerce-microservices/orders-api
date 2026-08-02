using AutoMapper;
using eCommerce.BLL.DTO.OrderItem;
using eCommerce.BLL.DTO.ProductsMicroservice;
using eCommerce.DAL.Entities;

namespace eCommerce.BLL.Mappers;

public class OrderItemMappingProfile : Profile
{
    public OrderItemMappingProfile()
    {
        CreateMap<OrderItemAddRequest, OrderItem>();

        CreateMap<OrderItemUpdateRequest, OrderItem>();

        CreateMap<OrderItem, OrderItemResponse>();

        CreateMap<ProductDTO, OrderItemResponse>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(dest => dest.Quantity, opt => opt.Ignore())
            .ForMember(dest => dest.TotalPrice, opt => opt.Ignore())
            .ForMember(dest => dest.ProductID, opt => opt.Ignore());

    }
}
