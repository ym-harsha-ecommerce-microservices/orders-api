namespace eCommerce.BLL.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException()
        : base("The request is invalid.")
    {
    }

    public BadRequestException(string message)
        : base(message)
    {
    }

    public BadRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}