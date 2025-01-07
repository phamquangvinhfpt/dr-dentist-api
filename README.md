[![.NET - 7](https://img.shields.io/badge/.NET-7-blue?logo=dotnet)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) [![.NET - 7](https://img.shields.io/badge/PG-16-blue?logo=postgresql)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

[![dotnet-cicd](https://github.com/phamquangvinhfpt/dr-dentist-api/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phamquangvinhfpt/dr-dentist-api/actions/workflows/dotnet.yml)

<p align="center">
  <a href="#" target="_blank">
    <img alt="DCMS Logo" width="250" src="./public/1.png">
  </a>
</p>

# DCMS-NET-API

This is a project to build a RESTful API for the DCMS-NET project. The project is built on .NET 7 and PostgreSQL 16.

## Getting started

To make it easy for you to get started with project, here's a list of recommended next steps.

## Before getting started

### Install Environment:

- https://dotnet.microsoft.com/download/dotnet/7.0
- https://visualstudio.microsoft.com/

## Clone repository

``` bash
git clone https://github.com/phamquangvinhfpt/dr-dentist-api.git
cd DR-DENTIST-API
dotnet run --project .\src\Host\Host.csproj --configuration Release
```

## Application URL
- Local environment: https://localhost:5001/swagger/index.html
- Production environment: https://api.drdentist.me/swagger/index.html

### Account for testing: All same pass 123Pa$$word!

- Admin: admin@root.com
- Staff: staff@root.com
- Doctor: dentist@root.com
- Patient: patient@root.com
- Patient for testing: patient1-4@root.com

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