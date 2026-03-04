# Itemite API

Itemite API is a backend system for an online classifieds platform built with ASP.NET Core. The project includes advertisement management, real-time chat functionality between users, dynamic pricing configuration for listings, user account management with real email handling, product purchases simulation using Stripe and secure authentication and authorization mechanisms. Created to use with [ItemiteClient](https://github.com/filip-wojc/ItemiteClient).

## Tech Stack

### Core Technologies

- **ASP.NET Core 9.0** - Web API framework
- **ASP.NET Identity** - Authentication and Authorization framework
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

### External Integrations

- **Google OAuth** - Authentication via Google
- **Cloudinary** - Media storage
- **Stripe** - Simulation of real payments
- **SMTP Email Service** - Email integration

### Messaging                                                                                                                                                                                                                                                                                                                       
- **RabbitMQ** - Message broker (integrated via MassTransit for event-driven side effects; added post-completion for learning purposes)

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
## API Endpoints

All endpoints are prefixed with `/api`. Authentication uses JWT Bearer tokens.

### Auth — `api/auth`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/register` | Public | Register a new user account |
| `POST` | `/login` | Public (rate limited) | Login with email and password |
| `GET` | `/login/google` | Public | Initiate Google OAuth login |
| `GET` | `/login/google/callback` | Public | Google OAuth callback handler |
| `POST` | `/refresh` | Public | Refresh access token |
| `GET` | `/logout` | Public | Logout from current device |
| `GET` | `/logout-all-devices` | Public | Logout from all devices |
| `GET` | `/confirm-email` | Public | Confirm email address |
| `POST` | `/forgot-password` | Public | Request a password reset email |
| `POST` | `/reset-password` | Public | Reset password using a token |

---

### User — `api/user`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/me` | Required | Get current authenticated user |
| `GET` | `/{userId}` | Public | Get public profile of a user |
| `POST` | `/profile/picture` | Required | Upload profile picture |
| `DELETE` | `/profile/picture` | Required | Delete profile picture |
| `POST` | `/profile/background` | Required | Upload profile background image |
| `DELETE` | `/profile/background` | Required | Delete profile background image |
| `PUT` | `/settings/change-email` | Required | Request email address change |
| `GET` | `/settings/confirm-email-change` | Required | Confirm email address change |
| `POST` | `/settings/change-password` | Required | Change account password |
| `PUT` | `/settings/change-phone-number` | Required | Update phone number |
| `PUT` | `/settings/change-username` | Required | Update username |
| `PUT` | `/settings/change-location` | Required | Update location |

---

### Listings — `api/listing`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/` | Public | Get paginated list of all listings |
| `GET` | `/{userId}` | Public | Get listings by a specific user |
| `GET` | `/dedicated` | Public | Get dedicated/promoted listings |
| `GET` | `/follow` | Required | Get listings followed by current user |
| `POST` | `/feature` | Required | Feature a listing |
| `PUT` | `/archive/{listingId}` | Required | Archive a listing |
| `POST` | `/follow/{listingId}` | Required | Follow a listing |
| `DELETE` | `/follow/{listingId}` | Required | Unfollow a listing |

---

### Product Listings — `api/productlisting`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/{listingId}` | Public | Get product listing details |
| `POST` | `/` | Required | Create a new product listing |
| `PUT` | `/{listingId}` | Required | Update a product listing |
| `POST` | `/{listingId}/user-price/{userId}` | Required | Set a custom price for a specific user |
| `DELETE` | `/{listingId}/user-price/{userId}` | Required | Remove custom price for a specific user |

---

### Auction Listings — `api/auctionlisting`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/{listingId}` | Public | Get auction listing details |
| `POST` | `/` | Required | Create a new auction listing |
| `PUT` | `/{listingId}` | Required | Update an auction listing |
| `POST` | `/{listingId}/bid` | Required | Place a bid on an auction |
| `GET` | `/{listingId}/bid` | Public | Get bid history for an auction |

---

### Categories — `api/categories`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/all` | Public | Get all categories |
| `GET` | `/main` | Public | Get top-level categories |
| `GET` | `/sub/{parentId}` | Public | Get subcategories of a category |
| `GET` | `/tree/{rootCategoryId}` | Public | Get full category tree from a root |

---

### Messages — `api/message`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/` | Required | Send a message |
| `PUT` | `/{messageId}` | Required | Edit a message |
| `DELETE` | `/{messageId}` | Required | Delete a message |
| `GET` | `/{listingId}/chats` | Required | Get all chats for a listing |
| `GET` | `/chats` | Required | Get all chats of current user |
| `GET` | `/{listingId}/chats/{otherUserId}` | Required | Get chat with a specific user about a listing |

---

### Notifications — `api/notification`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/` | Required | Get notifications for current user |
| `GET` | `/unread-count` | Required | Get count of unread notifications |
| `DELETE` | `/{notificationId}` | Required | Delete a single notification |
| `DELETE` | `/` | Required | Delete all notifications |

---

### Payments — `api/payment`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/stripe/connect/start` | Required | Start Stripe Connect onboarding |
| `GET` | `/stripe/connect/refresh-onboarding-link` | Public | Refresh Stripe onboarding link |
| `GET` | `/stripe/onboarding-status` | Required | Get Stripe onboarding status |
| `POST` | `/purchase-product/{productListingId}` | Required | Purchase a product listing |
| `GET` | `/my-purchases` | Required | Get purchases made by current user |
| `GET` | `/my-sales` | Required | Get sales made by current user |
| `POST` | `/confirm-delivery/{listingId}` | Required | Confirm delivery of a purchase |
| `POST` | `/dispute/{paymentId}` | Required | Open a dispute for a payment |

---

### Reports — `api/report`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/` | Required (rate limited) | Submit a report against a listing or user |

---

### Banners — `api/banner`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/active` | Public | Get all currently active banners |

---

### Admin Panel — `api/adminpanel`

> Requires `Admin` or `Moderator` role.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/category` | Create category |
| `PUT` | `/category/{categoryId}` | Update category |
| `DELETE` | `/category/{categoryId}` | Delete category |
| `POST` | `/global-notification` | Send global notification |
| `POST` | `/notification/{userId}` | Send notification to user |
| `DELETE` | `/{listingId}` | Delete listing |
| `GET` | `/reports` | Get reports |
| `POST` | `/lock-user` | Lock user account |
| `POST` | `/unlock-user` | Unlock user account |
| `GET` | `/users` | Get all users |
| `GET` | `/payments/with-status` | Get payments by status |
| `GET` | `/payments/latest` | Get latest payments |
| `GET` | `/payments/counts` | Get payment counts by status |
| `POST` | `/dispute/resolve/{disputeId}` | Resolve a dispute |
| `POST` | `/banners` | Add banner |
| `PUT` | `/banners/{bannerId}` | Update banner |
| `POST` | `/banners/active/{bannerId}` | Toggle banner active state |
| `DELETE` | `/banners/{bannerId}` | Delete banner |
| `GET` | `/banners/all` | Get all banners |

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
      POSTGRES_USER: user
      POSTGRES_PASSWORD: password
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
  rabbitmq:
    image: rabbitmq:3-management
    container_name: itemite-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
volumes:
  postgres-data:
  redis-data:
  rabbitmq-data:
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


