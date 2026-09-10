using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions
{
    public class MenuFunctions
    {
        private readonly ILogger _logger;

        public MenuFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MenuFunctions>();
        }

        private TableClient GetTableClient()
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var tableClient = new TableClient(connectionString, "MenuItems");
            tableClient.CreateIfNotExists();
            return tableClient;
        }

        // 1. POST /api/menu - Create Menu Item
        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequestData req)
        {
            _logger.LogInformation("Creating new menu item");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var newItem = JsonSerializer.Deserialize<MenuItem>(requestBody);

                if (string.IsNullOrEmpty(newItem.PartitionKey) || string.IsNullOrEmpty(newItem.RowKey))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("PartitionKey and RowKey are required");
                    return badResponse;
                }

                var tableClient = GetTableClient();
                var entity = new TableEntity(newItem.PartitionKey, newItem.RowKey)
                {
                    ["Name"] = newItem.Name,
                    ["Description"] = newItem.Description,
                    ["Price"] = newItem.Price,
                    ["IsAvailable"] = newItem.IsAvailable
                };

                await tableClient.AddEntityAsync(entity);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteStringAsync("Menu item created successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // 2. GET /api/menu - Get All Menu Items
        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all menu items");

            try
            {
                var tableClient = GetTableClient();
                var results = new List<MenuItem>();

                await foreach (var entity in tableClient.QueryAsync<MenuItem>())
                {
                    results.Add(entity);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(JsonSerializer.Serialize(results));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // 3. GET /api/menu/category/{category} - Get by Category
        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequestData req,
            string category)
        {
            _logger.LogInformation($"Getting menu items for category: {category}");

            try
            {
                var tableClient = GetTableClient();
                var results = new List<MenuItem>();

                await foreach (var entity in tableClient.QueryAsync<MenuItem>(filter: $"PartitionKey eq '{category}'"))
                {
                    results.Add(entity);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(JsonSerializer.Serialize(results));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // 4. PUT /api/menu/{category}/{id} - Update Menu Item
        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            _logger.LogInformation($"Updating menu item: {category}/{id}");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var updateData = JsonSerializer.Deserialize<MenuItem>(requestBody);

                var tableClient = GetTableClient();
                var existing = await tableClient.GetEntityAsync<MenuItem>(category, id);

                if (existing.Value == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Menu item not found");
                    return notFound;
                }

                // Update only Price and IsAvailable
                existing.Value.Price = updateData.Price;
                existing.Value.IsAvailable = updateData.IsAvailable;

                await tableClient.UpdateEntityAsync(existing.Value, existing.Value.ETag);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Menu item updated successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // 5. DELETE /api/menu/{category}/{id} - Delete Menu Item
        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            _logger.LogInformation($"Deleting menu item: {category}/{id}");

            try
            {
                var tableClient = GetTableClient();
                await tableClient.DeleteEntityAsync(category, id);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Menu item deleted successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}