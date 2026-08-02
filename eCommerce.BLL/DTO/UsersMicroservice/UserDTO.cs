using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.BLL.DTO.UsersMicroservice;

public class UserDTO
{
    public Guid UserID { get; set; }
    public string? Email { get; set; }
    public string? PersonName { get; set; }
    public string? Gender { get; set; }
}

