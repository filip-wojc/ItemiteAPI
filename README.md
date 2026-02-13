# Itemite API

Itemite API is a backend system for an online classifieds platform built with ASP.NET Core. The project includes advertisement management, real-time chat functionality between users, dynamic pricing configuration for listings, user account management with real email handling, product purchases simulation using Stripe and secure authentication and authorization mechanisms. Created to use with [ItemiteClient](https://github.com/filip-wojc/ItemiteClient).

## Tech Stack

### Core Technologies

- **ASP.NET Core 9.0** - Web API framework
- **SignalR** - Real-time web functionality
- **Entity Framework Core** - ORM with PostgreSQL
- **MediatR** - CQRS pattern implementation
- **AutoMapper** - Object-to-object mapping
- **FluentValidation** - Input validation

### Databases

- **PostgreSQL** - Primary database 
- **Redis** - Data caching and connection tracking

### DevOps & Monitoring
- **Docker & Docker Compose** - Containerization
- **Serilog** - Structured logging with custom themes
- **Swagger** - API documentation

### Authentication

- **JWT Bearer** - Token-based authentication
- **Refresh Tokens** - Secure token renewal
- **ASP.NET Identity** - Identity framework

### External Integrations

- **Google OAuth** - Authentication via Google
- **Cloudinary** - Media storage
- **Stripe** - Simulation of real payments
- **SMTP Email Service** - Email integration

## Architecture

Built using **Clean Architecture** principles with clear separation of concerns:

```
┌─────────────────┐
│   API Layer     │  ← Controllers, Middlewares
├─────────────────┤
│ Application     │  ← MediatR Commands/Queries, Validators,  Mappers
├─────────────────┤
│ Infrastructure  │  ← SignalR Hubs, Services, Database, External APIs, Repositories
├─────────────────┤
│   Domain        │  ← Entities, DTOs , Enums, Value objects
└─────────────────┘

Each layer has it's own extension functions to register/configure their respective services.
Each layer has it's own specific exceptions defined.
```
## API endpoints

### Admin Panel
<img width="1493" height="1134" alt="image" src="https://github.com/user-attachments/assets/68f2776f-532c-4806-9e8d-ad7abc48d48c" />

### Auction Listing
<img width="1466" height="333" alt="image" src="https://github.com/user-attachments/assets/26f49624-66a6-4e27-82ce-ba9eae0c367f" />

### Authentication
<img width="1516" height="607" alt="image" src="https://github.com/user-attachments/assets/ccc21d7c-d295-4eb1-81cf-013adab72908" />

### Banners
<img width="1479" height="103" alt="image" src="https://github.com/user-attachments/assets/7329de40-1e37-4314-8a65-4f025c094a1a" />

### Categories
<img width="1479" height="284" alt="image" src="https://github.com/user-attachments/assets/a30dab92-b232-4fc4-8dd2-7e8e0f59c5ba" />

### Listing
<img width="1497" height="501" alt="image" src="https://github.com/user-attachments/assets/547c4708-db25-47bf-9888-c60ede883dde" />

### Messages
<img width="1479" height="402" alt="image" src="https://github.com/user-attachments/assets/a4164c82-9ad5-4150-aaba-306ed1ceb5bd" />

### Notifications
<img width="1451" height="281" alt="image" src="https://github.com/user-attachments/assets/17ae5f17-3cef-457b-9a4b-8623b212602e" />

### Payment
<img width="1456" height="512" alt="image" src="https://github.com/user-attachments/assets/db133916-0a0a-4baa-bd3d-1627e5aa9973" />

### Product Listing
<img width="1445" height="328" alt="image" src="https://github.com/user-attachments/assets/cf9de80d-7f7d-44de-93d6-94b1bccda53b" />

### Report
<img width="1444" height="121" alt="image" src="https://github.com/user-attachments/assets/c0539929-373f-4592-9923-9f9354fd8a66" />

### User
<img width="1467" height="732" alt="image" src="https://github.com/user-attachments/assets/f10e5bb7-041b-4207-a513-c75eaeaa536a" />


## How to run

### 1. Clone the repository

```bash
git clone <repository-url>
cd ItemiteAPI
```
### 2. Install required tools

Make sure you have installed:
- .NET SDK 9.0
- .NET SDK 8.0 (required by some dependencies/tools)
- Entity Framework Core CLI (version 9.0)

Install EF Core CLI globally:
```bash
dotnet tool install --global dotnet-ef --version 9.0.0
```

Verify installation:
```bash
dotnet ef
```

### 3. Configure Docker services
Edit `docker-compose.yml` and set your own PostgreSQL credentials:
```yml
version: '3.8'

services:
  postgres:
    image: postgres:latest
    container_name: itemite-postgres
    environment:
      POSTGRES_DB: ItemiteDB
      POSTGRES_USER: <your-postgres-username>
      POSTGRES_PASSWORD: <your-postgres-password>
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql

  redis:
    image: redis:latest
    container_name: itemite-redis
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

volumes:
  postgres-data:
  redis-data:

```
The database credentials must match the connection string in `appsettings.Development.json`.

### 4. Configure application settings

Open `appsettings.Development.json`

This file contains all configurable application settings, including:
- JWT authentication
- Email & SMTP configuration
- Google OAuth authentication
- Stripe payments
- Cloudinary media storage
- Seeding options
- Redirect URLs
- CORS configuration

By default, Urls pointing to `http://localhost:4200` refer to Itemite Client (Angular frontend). If your frontend runs on a different address — update these values accordingly.

External services configuration (Required)
The following sections must be filled with your own credentials:
- GoogleOAuth → Your Google OAuth Client ID & Client Secret
- CloudinarySettings → Your Cloudinary Cloud Name, API Key, API Secret
- StripeSettings → Your Stripe Secret Key
- SMTPSettings → SMTP host, port, username and password
- EmailSettings → Email address used to send system emails

### 5. Start infrastructure (PostgreSQL + Redis)
```bash
docker compose up -d
```
Verify if containers are running

### 6. Apply database migrations
```bash
dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj --context Infrastructure.Database.ItemiteDbContext --configuration Debug <target-migration> --connection <database-connection-string>
```

### 7. Run the application
```bash
dotnet run --project API
```
The API should be available at: `http://localhost:5066`
Swagger documentation is available at: `http://localhost:5066/swagger`

## License

This project is part of an academic project by [filip_wojc](https://github.com/filip-wojc), [DarknesoPirate](https://github.com/DarknessoPirate),  [RobertPintera](https://github.com/RobertPintera), [HAScrashed](https://github.com/HAScrashed)

















