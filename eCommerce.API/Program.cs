using eCommerce.BLL;
using eCommerce.DAL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBusinessLogicLayer();
builder.Services.AddDataAccessLayer(builder.Configuration);


builder.Services.Configure


var app = builder.Build();

app.UseRouting();

app.MapControllers();


app.Run();
