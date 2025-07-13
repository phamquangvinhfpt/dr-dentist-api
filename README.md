# 🦷 DrDentist - Dental Clinic Management System API 

[![.NET - 7](https://img.shields.io/badge/.NET-7-blue?logo=dotnet)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) 
[![PostgreSQL - 16](https://img.shields.io/badge/PostgreSQL-16-blue?logo=postgresql)](https://www.postgresql.org/) 
[![Docker](https://img.shields.io/badge/Docker-Ready-blue?logo=docker)](https://www.docker.com/)
[![Redis](https://img.shields.io/badge/Redis-Cache-red?logo=redis)](https://redis.io/)
[![dotnet-cicd](https://github.com/phamquangvinhfpt/dr-dentist-api/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phamquangvinhfpt/dr-dentist-api/actions/workflows/dotnet.yml)

<p align="center">
  <a href="#" target="_blank">
    <img alt="DCMS Logo" width="250" src="./public/1.png">
  </a>
</p>

## 📋 Overview

DrDentist is a comprehensive Dental Clinic Management System (DCMS) built as a RESTful API using .NET 7 and PostgreSQL 16. This system is designed to streamline dental clinic operations, including appointment management, patient records, treatment plans, payments, and more.

## ✨ Features

- 👥 **Multi-tenant architecture** - Support for multiple dental clinics
- 👤 **Role-based access control** - Different permissions for admin, staff, doctors, and patients
- 📅 **Appointment management** - Schedule, reschedule, and cancel appointments
- 📁 **Patient medical records** - Store and retrieve patient information and treatment history
- 💼 **Treatment plans** - Create and manage dental treatment plans
- 💰 **Payment processing** - Handle payments and generate invoices
- 💬 **Real-time notifications** - Using SignalR for instant updates
- 🔄 **Background processing** - Using Hangfire for scheduled tasks
- 🗄️ **Caching** - Redis-based distributed caching
- 📧 **Email notifications** - Automated emails for appointments and more

## 🚀 Getting Started

### Prerequisites

Before you begin, make sure you have the following installed:

- [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- [Visual Studio](https://visualstudio.microsoft.com/) or another IDE of your choice
- [PostgreSQL 16](https://www.postgresql.org/download/)
- [Redis](https://redis.io/docs/latest/operate/oss_and_stack/install/install-redis/) (for caching and SignalR backplane)

### Installation

1. **Clone the repository**
```bash
git clone https://github.com/phamquangvinhfpt/dr-dentist-api.git
cd dr-dentist-api
```

2. **Set up Redis using Docker**
```bash
docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 redis/redis-stack:latest
```

3. **Configure the connection strings**

Update the following configuration files:

- **database.json**
```json
{
  "DatabaseSettings": {
    "DBProvider": "postgresql",
    "ConnectionString": "Host=localhost;Port=5432;Database=dcms-db-test;Username=postgres;Password=12345;Include Error Detail=true"
  }
}
```

- **hangfire.json**
```json
{
  "HangfireSettings": {
    "Storage": {
      "StorageProvider": "postgresql",
      "ConnectionString": "Host=localhost;Port=5432;Database=dcms-db-test;Username=postgres;Password=12345;Include Error Detail=true",
      "Options": {
        "CommandBatchMaxTimeout": "00:05:00",
        "QueuePollInterval": "00:00:01",
        "UseRecommendedIsolationLevel": true,
        "SlidingInvisibilityTimeout": "00:05:00",
        "DisableGlobalLocks": true
      }
    }
  }
}
```

- **signalr.json**
```json
{
  "SignalRSettings": {
    "UseBackplane": true,
    "Backplane": {
      "Provider": "redis",
      "StringConnection": "localhost:6379"
    }
  }
}
```

- **cache.json**
```json
{
  "CacheSettings": {
    "UseDistributedCache": true,
    "PreferRedis": true,
    "RedisURL": "localhost:6379"
  }
}
```

4. **Run the application**
```bash
dotnet run --project .\src\Host\Host.csproj --configuration Release
```

## 🌐 API Access

- **Local environment**: https://localhost:5001/swagger/index.html
- **Production environment**: https://api.drdentist.site/swagger/index.html

## 🔑 Test Accounts

All accounts share the same password: `123Pa$$word!`

| Role | Email | Description |
|------|-------|-------------|
| Admin | admin@root.com | Full system access |
| Staff | staff@root.com | Clinic management access |
| Doctor | dentist@root.com | Medical staff access |
| Patient | patient@root.com | Patient portal access |
| Test Patients | patient1-4@root.com | Additional test patient accounts |

### Authentication Example

```bash
curl -X 'POST' \
  'https://localhost:5001/api/tokens' \
  -H 'accept: application/json' \
  -H 'tenant: root' \
  -H 'Content-Type: application/json' \
  -d '{
  "email": "admin@root.com",
  "password": "123Pa$$word!",
  "captchaToken": "9PA}rTVa^9*1tCyiNTk?ix=.dq)6kW",
  "deviceId": "web"
}'
```

## 🏗️ Architecture

The project follows Clean Architecture principles with these main components:

- **Core**
  - **Domain**: Business entities and logic
  - **Application**: Application services, DTOs, and interfaces
  - **Shared**: Shared components and utilities

- **Infrastructure**: External systems implementation (database, email, etc.)

- **Host**: API endpoints and configurations

## 🤝 Contributing

Please read our [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 📞 Support

For support, please open an issue on the GitHub repository or contact the project maintainers.
