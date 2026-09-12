# CoffeeNChill Functions

Azure Functions project for the CoffeeNChill application.

## Project Overview

This project provides backend API functionality for managing menu items for the CoffeeNChill application.

The project is built using:

- .NET 8
- Azure Functions
- Azure Functions Isolated Worker
- Azure Table Storage
- Azurite for local storage development
- Postman for API testing

## Features

The Menu Functions API provides the following operations:

- Create a menu item
- Retrieve all menu items
- Retrieve menu items by category
- Update a menu item
- Delete a menu item

## Project Structure

```text
CoffeeNChill.Functionss/
│
├── CoffeeNChill.Functions/
│   ├── MenuItem.cs
│   ├── Program.cs
│   ├── Function files
│   ├── local.settings.json
│   └── ...
│
├── docs/
│   └── Postman collection
│
├── .gitignore
├── README.md
└── CoffeeNChill.Functions.sln
