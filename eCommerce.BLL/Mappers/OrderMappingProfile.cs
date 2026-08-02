using AutoMapper;
using eCommerce.BLL.DTO.Order;
using eCommerce.BLL.DTO.UsersMicroservice;
using eCommerce.DAL.Entities;

namespace eCommerce.BLL.Mappers;

public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<OrderAddRequest, Order>()
            .ForMember(dest => dest.OrderID, opt => opt.Ignore())
            .ForMember(dest => dest.UserID, opt => opt.MapFrom(src => src.UserID))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.TotalBill, opt => opt.Ignore())
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems));

        CreateMap<OrderUpdateRequest, Order>()
            .ForMember(dest => dest.OrderID, opt => opt.MapFrom(src => src.OrderID))
            .ForMember(dest => dest.UserID, opt => opt.MapFrom(src => src.UserID))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.TotalBill, opt => opt.Ignore())
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems));

        CreateMap<Order, OrderResponse>()
            .ForMember(dest => dest.OrderID, opt => opt.MapFrom(src => src.OrderID))
            .ForMember(dest => dest.UserID, opt => opt.MapFrom(src => src.UserID))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate ?? default))
            .ForMember(dest => dest.TotalBill, opt => opt.MapFrom(src => src.TotalBill ?? 0))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems));

        CreateMap<UserDTO, OrderResponse>()
            .ForMember(dest => dest.UserPersonName, opt => opt.MapFrom(src => src.PersonName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.OrderID, opt => opt.Ignore())
            .ForMember(dest => dest.UserID, opt => opt.Ignore())
            .ForMember(dest => dest.OrderDate, opt => opt.Ignore())
            .ForMember(dest => dest.TotalBill, opt => opt.Ignore())
            .ForMember(dest => dest.OrderItems, opt => opt.Ignore());
    }
}
