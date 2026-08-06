# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["eCommerce.API/eCommerce.API.csproj", "eCommerce.API/"]
COPY ["eCommerce.BLL/eCommerce.BLL.csproj", "eCommerce.BLL/"]
COPY ["eCommerce.DAL/eCommerce.DAL.csproj", "eCommerce.DAL/"]
RUN dotnet restore "./eCommerce.API/eCommerce.API.csproj"
COPY . .
WORKDIR "/src/eCommerce.API"
RUN dotnet build "./eCommerce.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./eCommerce.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_ENVIRONMENT="Development"
ENV DB_HOST="localhost"
ENV DB_PORT="27017"
ENV DB_NAME="eCommerceOrders"

ENV USERS_MICROSERVICE_NAME="localhost"
ENV USERS_MICROSERVICE_PORT="8000"

ENV PRODUCTS_MICROSERVICE_NAME="localhost"
ENV PRODUCTS_MICROSERVICE_PORT="9000"

ENV REDIS_HOST="localhost"
ENV REDIS_PORT="6379"

ENV RABBITMQ_HOST=rabbitmq
ENV RABBITMQ_PORT=5672
ENV RABBITMQ_USERNAME=guest
ENV RABBITMQ_PASSWORD=guest

ENV RABBITMQ_PRODUCT_EXCHANGE=product.exchange
ENV RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY=product.update.name
ENV RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY=product.delete
ENV RABBITMQ_PRODUCT_DELETE_QUEUE=product.delete.queue
ENV RABBITMQ_PRODUCT_UPDATE_QUEUE=product.update.queue

ENTRYPOINT ["dotnet", "eCommerce.API.dll"]